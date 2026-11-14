using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartTask.DAL.Identity;
using SmartTask.DAL.Persistence;
using SmartTask.Domain.Entities;
using SmartTask.DAL.Persistence.Repositories;

namespace SmartTask.IntegrationTests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(
                "SMARTTASK_TEST_CONNECTION_STRING")))
        {
            Skip = "Set SMARTTASK_TEST_CONNECTION_STRING to run SQL Server tests.";
        }
    }
}

public sealed class ProjectRowVersionConcurrencyTests
{
    [SqlServerFact]
    public async Task SaveChangesAsync_StaleProjectUpdate_ThrowsConcurrencyException()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "SMARTTASK_TEST_CONNECTION_STRING")!;

        var sqlConnection = new SqlConnectionStringBuilder(connectionString);

        Assert.Equal("SmartTaskTestDb", sqlConnection.InitialCatalog);

        var options = new DbContextOptionsBuilder<SmartTaskDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var ownerId = Guid.NewGuid().ToString();
        var projectId = Guid.NewGuid();
        var email = $"concurrency-{Guid.NewGuid():N}@example.com";

        try
        {
            await using (var setupContext = new SmartTaskDbContext(options))
            {
                setupContext.Users.Add(new ApplicationUser
                {
                    Id = ownerId,
                    UserName = email,
                    NormalizedUserName = email.ToUpperInvariant(),
                    Email = email,
                    NormalizedEmail = email.ToUpperInvariant(),
                    SecurityStamp = Guid.NewGuid().ToString(),
                    ConcurrencyStamp = Guid.NewGuid().ToString()
                });

                setupContext.Projects.Add(new Project
                {
                    Id = projectId,
                    Name = "Concurrency Test Project",
                    OwnerId = ownerId
                });

                await setupContext.SaveChangesAsync();
            }

            await using var contextA = new SmartTaskDbContext(options);
            await using var contextB = new SmartTaskDbContext(options);

            var projectA = await contextA.Projects
                .SingleAsync(p => p.Id == projectId);

            var projectB = await contextB.Projects
                .SingleAsync(p => p.Id == projectId);

            // Simulate the RowVersion supplied by the client.
            var clientRowVersion = projectB.RowVersion.ToArray();

            projectA.Name = "Updated by Context A";
            await contextA.SaveChangesAsync();

            // Apply the client's original version before attempting the update.
            var projectRepository = new ProjectRepository(contextB);

            projectRepository.SetOriginalRowVersion(
             projectB,
             clientRowVersion);



            projectB.Name = "Updated by Context B";

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
                () => contextB.SaveChangesAsync());
        }
        finally
        {
            await using var cleanupContext = new SmartTaskDbContext(options);

            var project = await cleanupContext.Projects
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(p => p.Id == projectId);

            if (project is not null)
            {
                cleanupContext.Projects.Remove(project);
            }

            var owner = await cleanupContext.Users
                .SingleOrDefaultAsync(u => u.Id == ownerId);

            if (owner is not null)
            {
                cleanupContext.Users.Remove(owner);
            }

            await cleanupContext.SaveChangesAsync();
        }
    }
}

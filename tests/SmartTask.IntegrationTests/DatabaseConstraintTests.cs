using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartTask.DAL.Identity;
using SmartTask.DAL.Persistence;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;
using TaskStatus = SmartTask.Domain.Enums.TaskStatus;

namespace SmartTask.IntegrationTests;

// Inserts go through raw SQL so the database constraints, not the
// application validators, are what reject invalid values.
public sealed class DatabaseConstraintTests
{
    private static DbContextOptions<SmartTaskDbContext> CreateOptions()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "SMARTTASK_TEST_CONNECTION_STRING")!;

        Assert.Equal(
            "SmartTaskTestDb",
            new SqlConnectionStringBuilder(connectionString).InitialCatalog);

        return new DbContextOptionsBuilder<SmartTaskDbContext>()
            .UseSqlServer(connectionString)
            .Options;
    }

    [SqlServerFact]
    public async Task Tasks_CheckConstraints_RejectInvalidStatusAndPriority()
    {
        var options = CreateOptions();
        var userId = Guid.NewGuid().ToString();
        var projectId = Guid.NewGuid();

        try
        {
            await SeedUserAndProjectAsync(options, userId, projectId);

            await using var context = new SmartTaskDbContext(options);

            var validTaskId = Guid.NewGuid();
            await InsertTaskAsync(
                context, validTaskId, projectId, userId,
                (int)TaskStatus.Cancelled, (int)TaskPriority.Critical);

            Assert.True(await context.Tasks
                .IgnoreQueryFilters()
                .AnyAsync(t => t.Id == validTaskId));

            foreach (var invalidStatus in new[] { 0, 5, 99 })
            {
                var exception = await Assert.ThrowsAsync<SqlException>(() =>
                    InsertTaskAsync(
                        context, Guid.NewGuid(), projectId, userId,
                        invalidStatus, (int)TaskPriority.Medium));

                Assert.Contains("CK_Tasks_Status", exception.Message);
            }

            foreach (var invalidPriority in new[] { 0, 5, -1 })
            {
                var exception = await Assert.ThrowsAsync<SqlException>(() =>
                    InsertTaskAsync(
                        context, Guid.NewGuid(), projectId, userId,
                        (int)TaskStatus.ToDo, invalidPriority));

                Assert.Contains("CK_Tasks_Priority", exception.Message);
            }

            var updateException = await Assert.ThrowsAsync<SqlException>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE [Tasks] SET [Status] = 42 WHERE [Id] = {validTaskId}"));

            Assert.Contains("CK_Tasks_Status", updateException.Message);
        }
        finally
        {
            await CleanupAsync(options, userId, projectId);
        }
    }

    [SqlServerFact]
    public async Task ProjectsAndMembers_CheckConstraints_RejectInvalidEnumValues()
    {
        var options = CreateOptions();
        var userId = Guid.NewGuid().ToString();
        var projectId = Guid.NewGuid();

        try
        {
            await SeedUserAndProjectAsync(options, userId, projectId);

            await using var context = new SmartTaskDbContext(options);

            var projectException = await Assert.ThrowsAsync<SqlException>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE [Projects] SET [Status] = 6 WHERE [Id] = {projectId}"));

            Assert.Contains("CK_Projects_Status", projectException.Message);

            var memberException = await Assert.ThrowsAsync<SqlException>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO [ProjectMembers] ([ProjectId], [UserId], [ProjectRole], [JoinedAtUtc])
                    VALUES ({projectId}, {userId}, {3}, SYSUTCDATETIME())
                    """));

            Assert.Contains("CK_ProjectMembers_ProjectRole", memberException.Message);
        }
        finally
        {
            await CleanupAsync(options, userId, projectId);
        }
    }

    [SqlServerFact]
    public async Task AuditLogs_UseBigintIdentityAndPreserveRequestIp()
    {
        var options = CreateOptions();
        var userId = Guid.NewGuid().ToString();
        var projectId = Guid.NewGuid();

        try
        {
            await SeedUserAndProjectAsync(options, userId, projectId);

            await using (var context = new SmartTaskDbContext(options))
            {
                context.AuditLogs.AddRange(
                    new AuditLog
                    {
                        UserId = userId,
                        Action = "ProjectCreated",
                        EntityName = "Project",
                        EntityId = projectId.ToString(),
                        IpAddress = "2001:db8:85a3:1234:5678:8a2e:370:7334"
                    },
                    new AuditLog
                    {
                        UserId = userId,
                        Action = "ProjectUpdated",
                        EntityName = "Project",
                        EntityId = projectId.ToString(),
                        IpAddress = "203.0.113.9"
                    });

                await context.SaveChangesAsync();
            }

            await using var readContext = new SmartTaskDbContext(options);

            var logs = await readContext.AuditLogs
                .Where(a => a.EntityName == "Project" &&
                            a.EntityId == projectId.ToString())
                .OrderBy(a => a.Id)
                .ToListAsync();

            Assert.Equal(2, logs.Count);
            Assert.True(logs[0].Id > 0);
            Assert.True(logs[1].Id > logs[0].Id);
            Assert.Equal("203.0.113.9", logs[1].IpAddress);
            Assert.All(logs, log =>
                Assert.True(log.TimestampUtc > DateTime.UtcNow.AddMinutes(-5)));
        }
        finally
        {
            await CleanupAsync(options, userId, projectId);
        }
    }

    private static Task<int> InsertTaskAsync(
        SmartTaskDbContext context,
        Guid taskId,
        Guid projectId,
        string userId,
        int status,
        int priority)
    {
        return context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO [Tasks]
                ([Id], [ProjectId], [Title], [Status], [Priority],
                 [CreatedByUserId], [CreatedAtUtc], [IsDeleted])
            VALUES
                ({taskId}, {projectId}, {"Constraint test"}, {status}, {priority},
                 {userId}, SYSUTCDATETIME(), 0)
            """);
    }

    private static async Task SeedUserAndProjectAsync(
        DbContextOptions<SmartTaskDbContext> options,
        string userId,
        Guid projectId)
    {
        var email = $"constraints-{Guid.NewGuid():N}@example.com";

        await using var context = new SmartTaskDbContext(options);

        context.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        });

        context.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Constraint Test Project",
            Status = ProjectStatus.Active,
            CreatedByUserId = userId
        });

        await context.SaveChangesAsync();
    }

    private static async Task CleanupAsync(
        DbContextOptions<SmartTaskDbContext> options,
        string userId,
        Guid projectId)
    {
        await using var context = new SmartTaskDbContext(options);

        await context.AuditLogs
            .Where(a => a.UserId == userId)
            .ExecuteDeleteAsync();

        await context.Tasks
            .IgnoreQueryFilters()
            .Where(t => t.ProjectId == projectId)
            .ExecuteDeleteAsync();

        await context.ProjectMembers
            .Where(m => m.ProjectId == projectId)
            .ExecuteDeleteAsync();

        await context.Projects
            .IgnoreQueryFilters()
            .Where(p => p.Id == projectId)
            .ExecuteDeleteAsync();

        await context.Users
            .Where(u => u.Id == userId)
            .ExecuteDeleteAsync();
    }
}

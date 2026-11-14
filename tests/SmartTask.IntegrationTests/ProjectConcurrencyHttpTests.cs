using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartTask.BLL.DTOs.Projects;
using SmartTask.DAL.Identity;
using SmartTask.DAL.Persistence;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace SmartTask.IntegrationTests;

public sealed class ProjectConcurrencyHttpTests
{
    [SqlServerFact]
    public async Task PutProject_WithStaleRowVersion_Returns409Conflict()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "SMARTTASK_TEST_CONNECTION_STRING")!;

        var connectionBuilder =
            new SqlConnectionStringBuilder(connectionString);

        Assert.Equal("SmartTaskTestDb", connectionBuilder.InitialCatalog);

        await using var factory =
            new ProjectTestWebApplicationFactory(connectionString);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
        using (var appScope = factory.Services.CreateScope())
        {
            var apiDb = appScope.ServiceProvider
                .GetRequiredService<SmartTaskDbContext>();

            Assert.Equal(
                "SmartTaskTestDb",
                apiDb.Database.GetDbConnection().Database);
        }
        var options = new DbContextOptionsBuilder<SmartTaskDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var ownerId = Guid.NewGuid().ToString();
        var projectId = Guid.NewGuid();
        var email = $"http-concurrency-{Guid.NewGuid():N}@example.com";

        try
        {
            byte[] originalRowVersion;

            // Arrange: create an owner, project, and owner membership.
            await using (var setupContext =
                new SmartTaskDbContext(options))
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

                var project = new Project
                {
                    Id = projectId,
                    Name = "Original Project",
                    OwnerId = ownerId
                };

                setupContext.Projects.Add(project);

                setupContext.ProjectMembers.Add(new ProjectMember
                {
                    ProjectId = projectId,
                    UserId = ownerId,
                    Role = ProjectMemberRole.Manager
                });

                await setupContext.SaveChangesAsync();

                originalRowVersion = project.RowVersion.ToArray();
            }

            // Simulate another request updating the project.
            await using (var concurrentContext =
                new SmartTaskDbContext(options))
            {
                var project = await concurrentContext.Projects
                    .SingleAsync(p => p.Id == projectId);

                project.Name = "Updated by another request";

                await concurrentContext.SaveChangesAsync();


            }
            // Verify the project still exists and belongs to the expected owner.
            await using (var verificationContext =
                new SmartTaskDbContext(options))
            {
                var persistedProject = await verificationContext.Projects
                    .SingleOrDefaultAsync(p => p.Id == projectId);

                Assert.NotNull(persistedProject);
                Assert.Equal(ownerId, persistedProject.OwnerId);
                Assert.False(persistedProject.IsDeleted);
            }
            // Act: submit the old RowVersion through the real HTTP endpoint.
            client.DefaultRequestHeaders.Add("X-Test-UserId", ownerId);

            var request = new UpdateProjectRequest
            {
                Name = "My stale update",
                Description = "This update must be rejected.",
                RowVersion = Convert.ToBase64String(originalRowVersion)
            };

            var response = await client.PutAsJsonAsync(
                $"/api/projects/{projectId}",
                request);

            // Assert: the API returns HTTP 409 Problem Details.
            var responseBody = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Contains("Concurrency conflict", responseBody);
            Assert.Contains("Reload it and try again", responseBody);
        }
        finally
        {
            // Remove dependent records first because the FKs restrict deletion.
            await using var cleanupContext =
                new SmartTaskDbContext(options);

            var memberships = await cleanupContext.ProjectMembers
                .Where(pm => pm.ProjectId == projectId)
                .ToListAsync();

            cleanupContext.ProjectMembers.RemoveRange(memberships);

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

internal sealed class ProjectTestWebApplicationFactory
    : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public ProjectTestWebApplicationFactory(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);

        if (builder.InitialCatalog != "SmartTaskTestDb")
        {
            throw new InvalidOperationException(
                "HTTP integration tests may only use SmartTaskTestDb.");
        }

        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        _connectionString,
                    ["JwtSettings:Issuer"] = "SmartTaskIntegrationTests",
                    ["JwtSettings:Audience"] = "SmartTaskIntegrationTests",
                    ["JwtSettings:SigningKey"] =
                        "IntegrationTestSigningKeyMustBeAtLeast32Chars",
                    ["JwtSettings:AccessTokenExpirationMinutes"] = "15"
                });
        });

        builder.ConfigureTestServices(services =>
 {
     // Use only the isolated test database.
     services.RemoveAll<SmartTaskDbContext>();
     services.RemoveAll<DbContextOptions<SmartTaskDbContext>>();

     services.AddDbContext<SmartTaskDbContext>(options =>
         options.UseSqlServer(_connectionString));

     services.AddAuthentication(options =>
             {
                 options.DefaultAuthenticateScheme =
                     TestAuthenticationHandler.SchemeName;

                 options.DefaultChallengeScheme =
                     TestAuthenticationHandler.SchemeName;
             })
             .AddScheme<AuthenticationSchemeOptions,
                 TestAuthenticationHandler>(
                 TestAuthenticationHandler.SchemeName,
                 _ => { });
 });
    }
}

internal sealed class TestAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ProjectIntegrationTest";

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers["X-Test-UserId"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        //Context.Response.Headers["X-Test-Authenticated-UserId"] = userId;

        var claims = new[]
        {
        new Claim(ClaimTypes.NameIdentifier, userId)
    };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

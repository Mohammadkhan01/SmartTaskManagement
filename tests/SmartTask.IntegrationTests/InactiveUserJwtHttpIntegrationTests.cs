using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartTask.BLL.Interfaces.Services;
using SmartTask.DAL.Identity;
using SmartTask.DAL.Persistence;

namespace SmartTask.IntegrationTests;

public sealed class InactiveUserJwtHttpIntegrationTests
{
    [SqlServerFact]
    public async Task GetCurrentUser_WhenAccountIsDeactivated_RejectsExistingJwt()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "SMARTTASK_TEST_CONNECTION_STRING")!;

        var connectionBuilder =
            new SqlConnectionStringBuilder(connectionString);

        Assert.Equal("SmartTaskTestDb", connectionBuilder.InitialCatalog);

        var options = new DbContextOptionsBuilder<SmartTaskDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var userId = Guid.NewGuid().ToString();
        var email = $"inactive-jwt-{Guid.NewGuid():N}@example.com";

        await using var factory =
            new InactiveUserJwtWebApplicationFactory(connectionString);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        try
        {
            // Arrange: create an active Identity user.
            await using (var setupContext =
                new SmartTaskDbContext(options))
            {
                setupContext.Users.Add(new ApplicationUser
                {
                    Id = userId,
                    UserName = email,
                    NormalizedUserName = email.ToUpperInvariant(),
                    Email = email,
                    NormalizedEmail = email.ToUpperInvariant(),
                    SecurityStamp = Guid.NewGuid().ToString(),
                    ConcurrencyStamp = Guid.NewGuid().ToString(),
                    IsActive = true
                });

                await setupContext.SaveChangesAsync();
            }

            // Generate a genuine JWT using the application's token service.
            string accessToken;

            using (var scope = factory.Services.CreateScope())
            {
                var tokenService = scope.ServiceProvider
                    .GetRequiredService<IJwtTokenService>();

                var token = tokenService.GenerateAccessToken(
                    userId,
                    email,
                    Array.Empty<string>());

                accessToken = token.AccessToken;


            }

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            // The active account must be allowed to authenticate.
            var activeResponse = await client.GetAsync("/api/v1/auth/me");

            Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);

            // Deactivate the account in the database.
            await using (var updateContext =
                new SmartTaskDbContext(options))
            {
                var user = await updateContext.Users
                    .SingleAsync(candidate => candidate.Id == userId);

                user.IsActive = false;

                await updateContext.SaveChangesAsync();
            }

            // Reuse the exact same, still-valid JWT.
            var inactiveResponse = await client.GetAsync("/api/v1/auth/me");

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                inactiveResponse.StatusCode);
        }
        finally
        {
            // Remove the test user.
            await using var cleanupContext =
                new SmartTaskDbContext(options);

            var user = await cleanupContext.Users
                .SingleOrDefaultAsync(candidate => candidate.Id == userId);

            if (user is not null)
            {
                cleanupContext.Users.Remove(user);
                await cleanupContext.SaveChangesAsync();
            }
        }
    }
}

internal sealed class InactiveUserJwtWebApplicationFactory
    : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public InactiveUserJwtWebApplicationFactory(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);

        if (builder.InitialCatalog != "SmartTaskTestDb")
        {
            throw new InvalidOperationException(
                "JWT integration tests may only use SmartTaskTestDb.");
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
                    ["Jwt:Issuer"] =
                        "SmartTaskIntegrationTests",
                    ["Jwt:Audience"] =
                        "SmartTaskIntegrationTests",
                    ["Jwt:SigningKey"] =
                        "IntegrationTestSigningKeyMustBeAtLeast32Chars",
                    ["Jwt:AccessTokenExpirationMinutes"] = "15"
                });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<SmartTaskDbContext>>();
            services.RemoveAll<SmartTaskDbContext>();

            services.AddDbContext<SmartTaskDbContext>(options =>
                options.UseSqlServer(_connectionString));
        });

        // Deliberately do not replace the JWT bearer authentication scheme.
        // This test must exercise the real OnTokenValidated event.
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SmartTask.API.HealthChecks;
using SmartTask.DAL.Persistence;

namespace SmartTask.IntegrationTests;

public sealed class DatabaseHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenDatabaseIsAvailable_ReturnsHealthy()
    {
        var options = new DbContextOptionsBuilder<SmartTaskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new SmartTaskDbContext(options);
        var healthCheck = new DatabaseHealthCheck(dbContext);

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext
            {
                Registration = new HealthCheckRegistration(
                    "database",
                    healthCheck,
                    HealthStatus.Unhealthy,
                    null)
            });

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDatabaseIsUnavailable_ReturnsUnhealthy()
    {
        var options = new DbContextOptionsBuilder<SmartTaskDbContext>()
            .UseSqlServer(
                "Server=127.0.0.1,1;Database=HealthCheckTests;Connect Timeout=1;Encrypt=False;TrustServerCertificate=True")
            .Options;

        await using var dbContext = new SmartTaskDbContext(options);
        var healthCheck = new DatabaseHealthCheck(dbContext);

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext
            {
                Registration = new HealthCheckRegistration(
                    "database",
                    healthCheck,
                    HealthStatus.Unhealthy,
                    null)
            });

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.DoesNotContain("127.0.0.1", result.Description);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy_WhenDatabaseIsAvailable()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddDbContext<SmartTaskDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        builder.Services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database");

        await using var app = builder.Build();
        app.MapHealthChecks("/health");
        await app.StartAsync();

        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/health");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", await response.Content.ReadAsStringAsync());
    }
}

using Microsoft.Extensions.Diagnostics.HealthChecks;
using SmartTask.DAL.Persistence;

namespace SmartTask.API.HealthChecks;

public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly SmartTaskDbContext _dbContext;

    public DatabaseHealthCheck(SmartTaskDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database
                .CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy("Database connection is available.")
                : HealthCheckResult.Unhealthy("Database connection is unavailable.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy(
                "Database connectivity check failed.");
        }
    }
}

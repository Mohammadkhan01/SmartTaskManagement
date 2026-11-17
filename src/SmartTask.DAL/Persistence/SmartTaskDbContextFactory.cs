using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartTask.DAL.Persistence;

public sealed class SmartTaskDbContextFactory
    : IDesignTimeDbContextFactory<SmartTaskDbContext>
{
    public SmartTaskDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "SMARTTASK_DESIGN_TIME_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set SMARTTASK_DESIGN_TIME_CONNECTION_STRING for EF design-time operations.");
        }

        var options = new DbContextOptionsBuilder<SmartTaskDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new SmartTaskDbContext(options);
    }
}

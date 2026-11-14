using Microsoft.EntityFrameworkCore;
using SmartTask.DAL.Persistence;
using SmartTask.Domain.Entities;

namespace SmartTask.UnitTests;

public class SmartTaskDbContextTests
{
    private static SmartTaskDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SmartTaskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new SmartTaskDbContext(options);
    }

    [Fact]
    public void SaveChanges_ShouldSetCreatedAtUtc_WhenEntityIsAdded()
    {
        using var context = CreateDbContext();

        var oldDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var auditLog = new AuditLog
        {
            Action = "Created",
            EntityType = "Project",
            CreatedAtUtc = oldDate,
            UpdatedAtUtc = oldDate
        };

        context.AuditLogs.Add(auditLog);
        context.SaveChanges();

        Assert.NotEqual(oldDate, auditLog.CreatedAtUtc);
        Assert.Null(auditLog.UpdatedAtUtc);
    }

    [Fact]
    public void SaveChanges_ShouldPreserveCreatedAtUtc_AndSetUpdatedAtUtc()
    {
        using var context = CreateDbContext();

        var auditLog = new AuditLog
        {
            Action = "Created",
            EntityType = "Project"
        };

        context.AuditLogs.Add(auditLog);
        context.SaveChanges();

        var originalCreatedAt = auditLog.CreatedAtUtc;

        auditLog.Action = "Updated";
        auditLog.CreatedAtUtc = new DateTime(
            2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        context.SaveChanges();

        Assert.Equal(originalCreatedAt, auditLog.CreatedAtUtc);
        Assert.NotNull(auditLog.UpdatedAtUtc);
        Assert.True(auditLog.UpdatedAtUtc > originalCreatedAt);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldSetCreatedAtUtc_WhenEntityIsAdded()
    {
        await using var context = CreateDbContext();

        var oldDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var auditLog = new AuditLog
        {
            Action = "Created",
            EntityType = "Project",
            CreatedAtUtc = oldDate
        };

        context.AuditLogs.Add(auditLog);
        await context.SaveChangesAsync();

        Assert.NotEqual(oldDate, auditLog.CreatedAtUtc);
        Assert.Null(auditLog.UpdatedAtUtc);
    }
}
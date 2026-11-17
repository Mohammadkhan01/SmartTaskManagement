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

        var notification = new Notification
        {
            RecipientUserId = "user-1",
            Type = "Created",
            CreatedAtUtc = oldDate,
            UpdatedAtUtc = oldDate
        };

        context.Notifications.Add(notification);
        context.SaveChanges();

        Assert.NotEqual(oldDate, notification.CreatedAtUtc);
        Assert.Null(notification.UpdatedAtUtc);
    }

    [Fact]
    public void SaveChanges_ShouldPreserveCreatedAtUtc_AndSetUpdatedAtUtc()
    {
        using var context = CreateDbContext();

        var notification = new Notification
        {
            RecipientUserId = "user-1",
            Type = "Created"
        };

        context.Notifications.Add(notification);
        context.SaveChanges();

        var originalCreatedAt = notification.CreatedAtUtc;

        notification.Type = "Updated";
        notification.CreatedAtUtc = new DateTime(
            2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        context.SaveChanges();

        Assert.Equal(originalCreatedAt, notification.CreatedAtUtc);
        Assert.NotNull(notification.UpdatedAtUtc);
        Assert.True(notification.UpdatedAtUtc > originalCreatedAt);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldSetCreatedAtUtc_WhenEntityIsAdded()
    {
        await using var context = CreateDbContext();

        var oldDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var notification = new Notification
        {
            RecipientUserId = "user-1",
            Type = "Created",
            CreatedAtUtc = oldDate
        };

        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        Assert.NotEqual(oldDate, notification.CreatedAtUtc);
        Assert.Null(notification.UpdatedAtUtc);
    }

    [Fact]
    public void SaveChanges_ShouldPersistAuditLogWithTimestampAndGeneratedId()
    {
        using var context = CreateDbContext();

        var before = DateTime.UtcNow;

        var auditLog = new AuditLog
        {
            Action = "ProjectCreated",
            EntityName = "Project",
            EntityId = Guid.NewGuid().ToString(),
            IpAddress = "203.0.113.10"
        };

        context.AuditLogs.Add(auditLog);
        context.SaveChanges();

        Assert.True(auditLog.Id > 0);
        Assert.True(auditLog.TimestampUtc >= before);
        Assert.Equal("203.0.113.10", auditLog.IpAddress);
    }
}

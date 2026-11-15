using Microsoft.EntityFrameworkCore;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.Domain.Entities;

namespace SmartTask.DAL.Persistence.Repositories;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly SmartTaskDbContext _context;

    public NotificationRepository(SmartTaskDbContext context)
    {
        _context = context;
    }

    public void Add(Notification notification)
    {
        _context.Notifications.Add(notification);
    }

    public Task<Notification?> GetByIdForRecipientAsync(
        Guid notificationId,
        string recipientUserId,
        CancellationToken cancellationToken = default)
    {
        return _context.Notifications.FirstOrDefaultAsync(
            notification =>
                notification.Id == notificationId &&
                notification.RecipientUserId == recipientUserId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>> GetPagedByRecipientAsync(
        string recipientUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .AsNoTracking()
            .Where(notification =>
                notification.RecipientUserId == recipientUserId)
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .ThenByDescending(notification => notification.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountByRecipientAsync(
        string recipientUserId,
        CancellationToken cancellationToken = default)
    {
        return _context.Notifications.CountAsync(
            notification =>
                notification.RecipientUserId == recipientUserId,
            cancellationToken);
    }

    public Task<int> CountUnreadByRecipientAsync(
        string recipientUserId,
        CancellationToken cancellationToken = default)
    {
        return _context.Notifications.CountAsync(
            notification =>
                notification.RecipientUserId == recipientUserId &&
                !notification.IsRead,
            cancellationToken);
    }

    public Task<int> MarkAllAsReadAsync(
        string recipientUserId,
        DateTime readAtUtc,
        CancellationToken cancellationToken = default)
    {
        return _context.Notifications
            .Where(notification =>
                notification.RecipientUserId == recipientUserId &&
                !notification.IsRead)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(notification => notification.IsRead, true)
                    .SetProperty(notification => notification.ReadAtUtc, readAtUtc)
                    .SetProperty(notification => notification.UpdatedAtUtc, readAtUtc),
                cancellationToken);
    }
}

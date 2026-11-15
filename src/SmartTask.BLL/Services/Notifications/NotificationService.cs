using SmartTask.BLL.DTOs.Notifications;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.BLL.Interfaces.Services;

namespace SmartTask.BLL.Services.Notifications;

public sealed class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public NotificationService(
        INotificationRepository notificationRepository,
        IUnitOfWork unitOfWork)
    {
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
    }

   public async Task<NotificationListResponse?> GetMyNotificationsAsync(
    string userId,
    int page,
    int pageSize,
    CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(userId) ||
        page < 1 ||
        pageSize < 1 ||
        pageSize > 100 ||
        (long)(page - 1) * pageSize > int.MaxValue)
    {
        return null;
    }

    var notifications =
        await _notificationRepository.GetPagedByRecipientAsync(
            userId,
            page,
            pageSize,
            cancellationToken);

    var totalCount =
        await _notificationRepository.CountByRecipientAsync(
            userId,
            cancellationToken);

    var unreadCount =
        await _notificationRepository.CountUnreadByRecipientAsync(
            userId,
            cancellationToken);

    return new NotificationListResponse
    {
        Items = notifications.Select(notification =>
            new NotificationResponse
            {
                Id = notification.Id,
                Type = notification.Type,
                Title = notification.Title,
                Message = notification.Message,
                RelatedEntityId = notification.RelatedEntityId,
                RelatedEntityType = notification.RelatedEntityType,
                IsRead = notification.IsRead,
                ReadAtUtc = notification.ReadAtUtc,
                CreatedAtUtc = notification.CreatedAtUtc
            }).ToList(),
        Page = page,
        PageSize = pageSize,
        TotalCount = totalCount,
        UnreadCount = unreadCount
    };
}

    public async Task<bool> MarkAsReadAsync(
        Guid notificationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (notificationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var notification =
            await _notificationRepository.GetByIdForRecipientAsync(
                notificationId,
                userId,
                cancellationToken);

        if (notification is null)
        {
            return false;
        }

        if (notification.IsRead)
        {
            return true;
        }

        var now = DateTime.UtcNow;

        notification.IsRead = true;
        notification.ReadAtUtc = now;
        notification.UpdatedAtUtc = now;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public Task<int> MarkAllAsReadAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(0);
        }

        return _notificationRepository.MarkAllAsReadAsync(
            userId,
            DateTime.UtcNow,
            cancellationToken);
    }
}

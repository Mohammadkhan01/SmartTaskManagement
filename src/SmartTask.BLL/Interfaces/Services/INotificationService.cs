using SmartTask.BLL.DTOs.Notifications;

namespace SmartTask.BLL.Interfaces.Services;

public interface INotificationService
{
    Task<NotificationListResponse?> GetMyNotificationsAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<bool> MarkAsReadAsync(
        Guid notificationId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllAsReadAsync(
        string userId,
        CancellationToken cancellationToken = default);
}

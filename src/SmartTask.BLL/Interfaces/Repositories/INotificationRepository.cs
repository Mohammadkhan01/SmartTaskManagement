using SmartTask.Domain.Entities;

namespace SmartTask.BLL.Interfaces.Repositories;

public interface INotificationRepository
{
    void Add(Notification notification);

    Task<Notification?> GetByIdForRecipientAsync(
        Guid notificationId,
        string recipientUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Notification>> GetPagedByRecipientAsync(
        string recipientUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> CountByRecipientAsync(
        string recipientUserId,
        CancellationToken cancellationToken = default);

    Task<int> CountUnreadByRecipientAsync(
        string recipientUserId,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllAsReadAsync(
        string recipientUserId,
        DateTime readAtUtc,
        CancellationToken cancellationToken = default);
}

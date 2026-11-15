namespace SmartTask.BLL.DTOs.Notifications;

public sealed class NotificationListResponse
{
    public IReadOnlyList<NotificationResponse> Items { get; set; } =
        Array.Empty<NotificationResponse>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int UnreadCount { get; set; }
}

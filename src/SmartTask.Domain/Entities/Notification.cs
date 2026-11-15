using SmartTask.Domain.Common;

namespace SmartTask.Domain.Entities;

public sealed class Notification : BaseEntity
{
    public string RecipientUserId { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public Guid? RelatedEntityId { get; set; }

    public string? RelatedEntityType { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAtUtc { get; set; }
}

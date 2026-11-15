using SmartTask.Domain.Common;

namespace SmartTask.Domain.Entities;

public class TaskAttachment : SoftDeletableEntity
{
    public Guid TaskId { get; set; }

    public string UploadedByUserId { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string StoredFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string StoragePath { get; set; } = string.Empty;

    public TaskItem Task { get; set; } = null!;
}

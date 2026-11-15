namespace SmartTask.BLL.DTOs.TaskAttachments;

public class TaskAttachmentResponse
{
    public Guid Id { get; set; }

    public Guid TaskId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string UploadedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}

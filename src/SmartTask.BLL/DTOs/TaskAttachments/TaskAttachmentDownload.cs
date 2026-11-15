namespace SmartTask.BLL.DTOs.TaskAttachments;

public sealed class TaskAttachmentDownload
{
    public required Stream Stream { get; init; }

    public required string OriginalFileName { get; init; }

    public required string ContentType { get; init; }
}
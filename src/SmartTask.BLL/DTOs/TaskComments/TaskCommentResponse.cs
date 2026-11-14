namespace SmartTask.BLL.DTOs.TaskComments;

public class TaskCommentResponse
{
    public Guid Id { get; set; }

    public Guid TaskId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public string RowVersion { get; set; } = string.Empty;
}

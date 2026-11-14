using SmartTask.Domain.Common;

namespace SmartTask.Domain.Entities;

public class TaskComment : SoftDeletableEntity
{
    public Guid TaskId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public TaskItem Task { get; set; } = null!;
}

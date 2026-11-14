using SmartTask.Domain.Common;
using SmartTask.Domain.Enums;

namespace SmartTask.Domain.Entities;

public class TaskItem : SoftDeletableEntity
{
    public Guid ProjectId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public SmartTask.Domain.Enums.TaskStatus Status { get; set; }
     = SmartTask.Domain.Enums.TaskStatus.ToDo;

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    // References the assigned Identity user ID.
    public string? AssignedToUserId { get; set; }

    // References the Identity user who created the task.
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime? DueDateUtc { get; set; }

    public Project Project { get; set; } = null!;
}
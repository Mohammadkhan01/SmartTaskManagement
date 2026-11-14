namespace SmartTask.BLL.DTOs.Tasks;

public class CreateTaskRequest
{
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Domain.Enums.TaskPriority Priority { get; set; }
        = Domain.Enums.TaskPriority.Medium;

    public string? AssignedToUserId { get; set; }

    public DateTime? DueDateUtc { get; set; }
}
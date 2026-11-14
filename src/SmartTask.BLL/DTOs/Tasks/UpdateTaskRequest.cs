namespace SmartTask.BLL.DTOs.Tasks;

public class UpdateTaskRequest
{
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Domain.Enums.TaskPriority Priority { get; set; }

    public string? AssignedToUserId { get; set; }

    public DateTime? DueDateUtc { get; set; }

    public string RowVersion { get; set; } = string.Empty;
}
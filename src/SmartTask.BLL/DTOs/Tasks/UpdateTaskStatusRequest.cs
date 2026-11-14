namespace SmartTask.BLL.DTOs.Tasks;

public class UpdateTaskStatusRequest
{
    public Domain.Enums.TaskStatus Status { get; set; }

    public string RowVersion { get; set; } = string.Empty;
}
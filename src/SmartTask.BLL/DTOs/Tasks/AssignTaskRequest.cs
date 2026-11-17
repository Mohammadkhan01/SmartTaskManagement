namespace SmartTask.BLL.DTOs.Tasks;

public class AssignTaskRequest
{
    public string AssignedToUserId { get; set; } = string.Empty;

    public string RowVersion { get; set; } = string.Empty;
}

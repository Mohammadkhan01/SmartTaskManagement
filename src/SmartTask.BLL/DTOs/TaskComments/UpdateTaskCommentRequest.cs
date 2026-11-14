namespace SmartTask.BLL.DTOs.TaskComments;

public class UpdateTaskCommentRequest
{
    public string Content { get; set; } = string.Empty;

    public string RowVersion { get; set; } = string.Empty;
}

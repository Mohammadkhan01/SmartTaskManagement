using SmartTask.BLL.DTOs.TaskComments;

namespace SmartTask.BLL.Interfaces.Services;

public interface ITaskActivityService
{
    Task<IReadOnlyList<TaskActivityResponse>?> GetByTaskIdAsync(
        Guid taskId,
        string userId,
        CancellationToken cancellationToken = default);
}

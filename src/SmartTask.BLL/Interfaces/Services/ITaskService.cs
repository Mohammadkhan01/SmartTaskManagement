using SmartTask.BLL.DTOs.Tasks;

namespace SmartTask.BLL.Interfaces.Services;

public interface ITaskService
{
    Task<IReadOnlyList<TaskResponse>?> GetByProjectIdAsync(
        Guid projectId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<TaskListResponse?> GetPagedByProjectIdAsync(
        Guid projectId,
        TaskListQueryRequest query,
        string userId,
        CancellationToken cancellationToken = default);

    Task<TaskResponse?> GetByIdAsync(
        Guid taskId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<TaskResponse?> CreateAsync(
        Guid projectId,
        CreateTaskRequest request,
        string userId,
        CancellationToken cancellationToken = default);

    Task<TaskResponse?> UpdateAsync(
        Guid taskId,
        UpdateTaskRequest request,
        string userId,
        CancellationToken cancellationToken = default);

    Task<TaskResponse?> AssignAsync(
        Guid taskId,
        AssignTaskRequest request,
        string userId,
        CancellationToken cancellationToken = default);

    Task<TaskResponse?> UpdateStatusAsync(
        Guid taskId,
        UpdateTaskStatusRequest request,
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid taskId,
        string userId,
        CancellationToken cancellationToken = default);
}

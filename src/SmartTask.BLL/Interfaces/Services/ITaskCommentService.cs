using SmartTask.BLL.DTOs.TaskComments;

namespace SmartTask.BLL.Interfaces.Services;

public interface ITaskCommentService
{
    Task<IReadOnlyList<TaskCommentResponse>?> GetByTaskIdAsync(
        Guid taskId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<TaskCommentResponse?> CreateAsync(
        Guid taskId,
        CreateTaskCommentRequest request,
        string userId,
        CancellationToken cancellationToken = default);

    Task<TaskCommentResponse?> UpdateAsync(
        Guid commentId,
        UpdateTaskCommentRequest request,
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid commentId,
        string userId,
        CancellationToken cancellationToken = default);
}

using SmartTask.Domain.Entities;

namespace SmartTask.BLL.Interfaces.Repositories;

public interface ITaskCommentRepository
{
    Task<TaskComment?> GetByIdAsync(
        Guid commentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskComment>> GetByTaskIdAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);

    void SetOriginalRowVersion(
        TaskComment comment,
        byte[] rowVersion);

    void Add(TaskComment comment);
}

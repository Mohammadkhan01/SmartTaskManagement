
using SmartTask.Domain.Entities;

namespace SmartTask.BLL.Interfaces.Repositories;

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskItem>> GetByProjectIdAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    void SetOriginalRowVersion(
        TaskItem task,
        byte[] rowVersion);

    void Add(TaskItem task);

    void Update(TaskItem task);
}

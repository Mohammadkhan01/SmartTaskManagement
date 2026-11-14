using SmartTask.Domain.Entities;

namespace SmartTask.BLL.Interfaces.Repositories;

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);

    void Add(TaskItem task);

    void Update(TaskItem task);
}

using SmartTask.Domain.Entities;

namespace SmartTask.BLL.Interfaces.Repositories;

public interface ITaskAttachmentRepository
{
    Task<TaskAttachment?> GetByIdAsync(
        Guid attachmentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskAttachment>> GetByTaskIdAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);

    void Add(TaskAttachment attachment);
}

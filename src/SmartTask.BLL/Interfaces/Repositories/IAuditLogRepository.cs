using SmartTask.Domain.Entities;

namespace SmartTask.BLL.Interfaces.Repositories;

public interface IAuditLogRepository
{
    Task<IReadOnlyList<AuditLog>> GetByTaskIdAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);

    void Add(AuditLog auditLog);
}

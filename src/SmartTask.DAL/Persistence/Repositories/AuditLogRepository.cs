using Microsoft.EntityFrameworkCore;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.Domain.Entities;

namespace SmartTask.DAL.Persistence.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly SmartTaskDbContext _context;

    public AuditLogRepository(SmartTaskDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AuditLog>> GetByTaskIdAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .AsNoTracking()
            .Where(a =>
                a.EntityType == "Task" &&
                a.EntityId == taskId.ToString())
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public void Add(AuditLog auditLog)
    {
        _context.AuditLogs.Add(auditLog);
    }
}

using Microsoft.EntityFrameworkCore;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.Domain.Entities;

namespace SmartTask.DAL.Persistence.Repositories;

public class TaskAttachmentRepository : ITaskAttachmentRepository
{
    private readonly SmartTaskDbContext _context;

    public TaskAttachmentRepository(SmartTaskDbContext context)
    {
        _context = context;
    }

    public Task<TaskAttachment?> GetByIdAsync(
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        return _context.TaskAttachments
            .FirstOrDefaultAsync(
                attachment => attachment.Id == attachmentId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<TaskAttachment>> GetByTaskIdAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        return await _context.TaskAttachments
            .AsNoTracking()
            .Where(attachment => attachment.TaskId == taskId)
            .OrderByDescending(attachment => attachment.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public void Add(TaskAttachment attachment)
    {
        _context.TaskAttachments.Add(attachment);
    }
}

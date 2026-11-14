using Microsoft.EntityFrameworkCore;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.Domain.Entities;

namespace SmartTask.DAL.Persistence.Repositories;

public class TaskCommentRepository : ITaskCommentRepository
{
    private readonly SmartTaskDbContext _context;

    public TaskCommentRepository(SmartTaskDbContext context)
    {
        _context = context;
    }

    public Task<TaskComment?> GetByIdAsync(
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        return _context.TaskComments
            .FirstOrDefaultAsync(
                c => c.Id == commentId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<TaskComment>> GetByTaskIdAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        return await _context.TaskComments
            .AsNoTracking()
            .Where(c => c.TaskId == taskId)
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public void SetOriginalRowVersion(
        TaskComment comment,
        byte[] rowVersion)
    {
        _context.Entry(comment)
            .Property(c => c.RowVersion)
            .OriginalValue = rowVersion;
    }

    public void Add(TaskComment comment)
    {
        _context.TaskComments.Add(comment);
    }
}

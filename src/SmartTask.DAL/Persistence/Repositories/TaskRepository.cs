
using Microsoft.EntityFrameworkCore;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.Domain.Entities;

namespace SmartTask.DAL.Persistence.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly SmartTaskDbContext _context;

    public TaskRepository(SmartTaskDbContext context)
    {
        _context = context;
    }

    public Task<TaskItem?> GetByIdAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        return _context.Tasks
            .FirstOrDefaultAsync(
                t => t.Id == taskId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<TaskItem>> GetByProjectIdAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Tasks
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public void SetOriginalRowVersion(
        TaskItem task,
        byte[] rowVersion)
    {
        _context.Entry(task)
            .Property(t => t.RowVersion)
            .OriginalValue = rowVersion;
    }

    public void Add(TaskItem task)
    {
        _context.Tasks.Add(task);
    }

    public void Update(TaskItem task)
    {
        _context.Tasks.Update(task);
    }
}

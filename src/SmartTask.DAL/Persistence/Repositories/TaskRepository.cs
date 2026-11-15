
using Microsoft.EntityFrameworkCore;
using SmartTask.BLL.DTOs.Tasks;
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

    public async Task<(IReadOnlyList<TaskItem> Items, int TotalCount)>
        GetPagedByProjectIdAsync(
            Guid projectId,
            TaskListQueryRequest query,
            CancellationToken cancellationToken = default)
    {
        IQueryable<TaskItem> tasks = _context.Tasks
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            tasks = tasks.Where(t =>
                t.Title.Contains(search) ||
                (t.Description != null && t.Description.Contains(search)));
        }

        if (query.Status.HasValue)
        {
            tasks = tasks.Where(t => t.Status == query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            tasks = tasks.Where(t => t.Priority == query.Priority.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.AssignedToUserId))
        {
            var assignedToUserId = query.AssignedToUserId.Trim();

            tasks = tasks.Where(t =>
                t.AssignedToUserId == assignedToUserId);
        }

        if (query.DueDateFromUtc.HasValue)
        {
            tasks = tasks.Where(t =>
                t.DueDateUtc >= query.DueDateFromUtc.Value);
        }

        if (query.DueDateToUtc.HasValue)
        {
            tasks = tasks.Where(t =>
                t.DueDateUtc <= query.DueDateToUtc.Value);
        }

        var totalCount = await tasks.CountAsync(cancellationToken);

        var sortBy = query.SortBy.Trim().ToLowerInvariant();

        IOrderedQueryable<TaskItem> orderedTasks = sortBy switch
        {
            "createdat" => query.SortDescending
                ? tasks.OrderByDescending(t => t.CreatedAtUtc)
                : tasks.OrderBy(t => t.CreatedAtUtc),

            "updatedat" => query.SortDescending
                ? tasks.OrderByDescending(t => t.UpdatedAtUtc)
                : tasks.OrderBy(t => t.UpdatedAtUtc),

            "duedate" => query.SortDescending
                ? tasks.OrderByDescending(t => t.DueDateUtc)
                : tasks.OrderBy(t => t.DueDateUtc),

            "title" => query.SortDescending
                ? tasks.OrderByDescending(t => t.Title)
                : tasks.OrderBy(t => t.Title),

            "priority" => query.SortDescending
                ? tasks.OrderByDescending(t => t.Priority)
                : tasks.OrderBy(t => t.Priority),

            "status" => query.SortDescending
                ? tasks.OrderByDescending(t => t.Status)
                : tasks.OrderBy(t => t.Status),

            _ => tasks.OrderByDescending(t => t.CreatedAtUtc)
        };

        orderedTasks = orderedTasks.ThenBy(t => t.Id);

        var skip = (int)((long)(query.PageNumber - 1) * query.PageSize);

        var items = await orderedTasks
            .Skip(skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
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

using Microsoft.EntityFrameworkCore;
using SmartTask.BLL.DTOs.Projects;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.DAL.Persistence;
using SmartTask.Domain.Entities;

namespace SmartTask.DAL.Persistence.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly SmartTaskDbContext _context;

    public ProjectRepository(SmartTaskDbContext context)
    {
        _context = context;
    }

    public Task<Project?> GetByIdAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return _context.Projects
            .FirstOrDefaultAsync(
                p => p.Id == projectId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Project>> GetByMemberUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Projects
            .AsNoTracking()
            .Where(p => p.Members.Any(m => m.UserId == userId))
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Project> Items, int TotalCount)>
        GetPagedByMemberUserIdAsync(
            string userId,
            ProjectListQueryRequest query,
            CancellationToken cancellationToken = default)
    {
        IQueryable<Project> projects = _context.Projects
            .AsNoTracking()
            .Where(p => p.Members.Any(m => m.UserId == userId));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            projects = projects.Where(p =>
                p.Name.Contains(search) ||
                (p.Description != null && p.Description.Contains(search)));
        }

        if (query.Status.HasValue)
        {
            projects = projects.Where(p => p.Status == query.Status.Value);
        }

        var totalCount = await projects.CountAsync(cancellationToken);
        var sortBy = query.SortBy.Trim().ToLowerInvariant();

        IOrderedQueryable<Project> orderedProjects = sortBy switch
        {
            "createdat" => query.SortDescending
                ? projects.OrderByDescending(p => p.CreatedAtUtc)
                : projects.OrderBy(p => p.CreatedAtUtc),
            "updatedat" => query.SortDescending
                ? projects.OrderByDescending(p => p.UpdatedAtUtc)
                : projects.OrderBy(p => p.UpdatedAtUtc),
            "name" => query.SortDescending
                ? projects.OrderByDescending(p => p.Name)
                : projects.OrderBy(p => p.Name),
            "status" => query.SortDescending
                ? projects.OrderByDescending(p => p.Status)
                : projects.OrderBy(p => p.Status),
            _ => projects.OrderByDescending(p => p.CreatedAtUtc)
        };

        var skip = (int)((long)(query.PageNumber - 1) * query.PageSize);

        var items = await orderedProjects
            .ThenBy(p => p.Id)
            .Skip(skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Project>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Projects
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Project> Items, int TotalCount)>
        GetPagedAllAsync(
            ProjectListQueryRequest query,
            CancellationToken cancellationToken = default)
    {
        IQueryable<Project> projects = _context.Projects.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            projects = projects.Where(p =>
                p.Name.Contains(search) ||
                (p.Description != null && p.Description.Contains(search)));
        }

        if (query.Status.HasValue)
        {
            projects = projects.Where(p => p.Status == query.Status.Value);
        }

        var totalCount = await projects.CountAsync(cancellationToken);
        var sortBy = query.SortBy.Trim().ToLowerInvariant();

        IOrderedQueryable<Project> orderedProjects = sortBy switch
        {
            "createdat" => query.SortDescending
                ? projects.OrderByDescending(p => p.CreatedAtUtc)
                : projects.OrderBy(p => p.CreatedAtUtc),
            "updatedat" => query.SortDescending
                ? projects.OrderByDescending(p => p.UpdatedAtUtc)
                : projects.OrderBy(p => p.UpdatedAtUtc),
            "name" => query.SortDescending
                ? projects.OrderByDescending(p => p.Name)
                : projects.OrderBy(p => p.Name),
            "status" => query.SortDescending
                ? projects.OrderByDescending(p => p.Status)
                : projects.OrderBy(p => p.Status),
            _ => projects.OrderByDescending(p => p.CreatedAtUtc)
        };

        var skip = (int)((long)(query.PageNumber - 1) * query.PageSize);

        var items = await orderedProjects
            .ThenBy(p => p.Id)
            .Skip(skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public void Add(Project project)
    {
        _context.Projects.Add(project);
    }

    public void Update(Project project)
    {
        _context.Projects.Update(project);
    }
    public void SetOriginalRowVersion(Project project, byte[] rowVersion)
    {
        _context.Entry(project)
            .Property(p => p.RowVersion)
            .OriginalValue = rowVersion;
    }
}
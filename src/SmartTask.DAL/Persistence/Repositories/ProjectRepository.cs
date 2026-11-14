using Microsoft.EntityFrameworkCore;
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
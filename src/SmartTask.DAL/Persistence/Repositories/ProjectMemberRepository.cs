using Microsoft.EntityFrameworkCore;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.Domain.Entities;

namespace SmartTask.DAL.Persistence.Repositories;

public class ProjectMemberRepository : IProjectMemberRepository
{
    private readonly SmartTaskDbContext _context;

    public ProjectMemberRepository(SmartTaskDbContext context)
    {
        _context = context;
    }

    public Task<ProjectMember?> GetByProjectAndUserAsync(
        Guid projectId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        return _context.ProjectMembers
            .FirstOrDefaultAsync(
                pm => pm.ProjectId == projectId &&
                      pm.UserId == userId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectMember>> GetByProjectIdAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ProjectMembers
            .AsNoTracking()
            .Where(pm => pm.ProjectId == projectId)
            .ToListAsync(cancellationToken);
    }

    public void Add(ProjectMember member)
    {
        _context.ProjectMembers.Add(member);
    }

    public void Remove(ProjectMember member)
    {
        _context.ProjectMembers.Remove(member);
    }
}

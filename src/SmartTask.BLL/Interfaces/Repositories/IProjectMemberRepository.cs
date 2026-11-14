using SmartTask.Domain.Entities;

namespace SmartTask.BLL.Interfaces.Repositories;

public interface IProjectMemberRepository
{
    Task<ProjectMember?> GetByProjectAndUserAsync(
        Guid projectId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectMember>> GetByProjectIdAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    void Add(ProjectMember member);

    void Remove(ProjectMember member);
}

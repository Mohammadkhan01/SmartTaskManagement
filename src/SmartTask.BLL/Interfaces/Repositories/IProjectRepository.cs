using SmartTask.BLL.DTOs.Projects;
using SmartTask.Domain.Entities;

namespace SmartTask.BLL.Interfaces.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Project>> GetByMemberUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Project> Items, int TotalCount)>
        GetPagedByMemberUserIdAsync(
            string userId,
            ProjectListQueryRequest query,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Project>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Project> Items, int TotalCount)>
        GetPagedAllAsync(
            ProjectListQueryRequest query,
            CancellationToken cancellationToken = default);

    void Add(Project project);

    void Update(Project project);

    void SetOriginalRowVersion(Project project, byte[] rowVersion);
}

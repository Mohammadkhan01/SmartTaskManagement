using SmartTask.BLL.DTOs.Projects;

namespace SmartTask.BLL.Interfaces.Services;

public interface IProjectService
{
    Task<ProjectResponse> CreateAsync(
        CreateProjectRequest request,
        string ownerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectResponse>> GetMyProjectsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<ProjectListResponse> GetPagedMyProjectsAsync(
        string userId,
        ProjectListQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ProjectResponse?> GetByIdAsync(
        Guid projectId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<ProjectResponse?> UpdateAsync(
        Guid projectId,
        UpdateProjectRequest request,
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid projectId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectMemberResponse>?> GetMembersAsync(
        Guid projectId,
        string requesterUserId,
        CancellationToken cancellationToken = default);

    Task<ProjectMemberResponse?> AddMemberAsync(
        Guid projectId,
        AddProjectMemberRequest request,
        string requesterUserId,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveMemberAsync(
        Guid projectId,
        string memberUserId,
        string requesterUserId,
        CancellationToken cancellationToken = default);
}

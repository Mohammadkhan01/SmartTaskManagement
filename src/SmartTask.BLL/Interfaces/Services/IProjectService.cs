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
}
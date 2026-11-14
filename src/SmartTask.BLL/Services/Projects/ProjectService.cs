using FluentValidation;
using SmartTask.BLL.DTOs.Projects;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.BLL.Interfaces.Services;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.BLL.Services.Projects;

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectMemberRepository _memberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateProjectRequest> _createValidator;
    private readonly IValidator<UpdateProjectRequest> _updateValidator;

    public ProjectService(
        IProjectRepository projectRepository,
        IProjectMemberRepository memberRepository,
        IUnitOfWork unitOfWork,
        IValidator<CreateProjectRequest> createValidator,
        IValidator<UpdateProjectRequest> updateValidator)
    {
        _projectRepository = projectRepository;
        _memberRepository = memberRepository;
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<ProjectResponse> CreateAsync(
        CreateProjectRequest request,
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException(
                "Owner ID is required.",
                nameof(ownerId));
        }

        var validationResult = await _createValidator.ValidateAsync(
            request,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var project = new Project
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            OwnerId = ownerId,
            Status = ProjectStatus.Planning
        };

        var managerMembership = new ProjectMember
        {
            ProjectId = project.Id,
            UserId = ownerId,
            Role = ProjectMemberRole.Manager,
            Project = project
        };

        _projectRepository.Add(project);
        _memberRepository.Add(managerMembership);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(project);
    }

    public async Task<IReadOnlyList<ProjectResponse>> GetMyProjectsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException(
                "User ID is required.",
                nameof(userId));
        }

        var projects = await _projectRepository.GetByMemberUserIdAsync(
            userId,
            cancellationToken);

        return projects.Select(MapToResponse).ToList();
    }

    public async Task<ProjectResponse?> GetByIdAsync(
        Guid projectId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var project = await _projectRepository.GetByIdAsync(
            projectId,
            cancellationToken);

        if (project is null)
        {
            return null;
        }

        var membership = await _memberRepository.GetByProjectAndUserAsync(
            projectId,
            userId,
            cancellationToken);

        if (membership is null)
        {
            return null;
        }

        return MapToResponse(project);
    }

    public async Task<ProjectResponse?> UpdateAsync(
        Guid projectId,
        UpdateProjectRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var project = await _projectRepository.GetByIdAsync(
            projectId,
            cancellationToken);

        if (project is null || project.OwnerId != userId)
        {
            return null;
        }

        var validationResult = await _updateValidator.ValidateAsync(
            request,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }
        var originalRowVersion = Convert.FromBase64String(
    request.RowVersion);

        _projectRepository.SetOriginalRowVersion(
            project,
            originalRowVersion);

        project.Name = request.Name.Trim();
        project.Description = request.Description?.Trim();



        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(project);
    }

    public async Task<bool> DeleteAsync(
        Guid projectId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var project = await _projectRepository.GetByIdAsync(
            projectId,
            cancellationToken);

        if (project is null || project.OwnerId != userId)
        {
            return false;
        }

        project.IsDeleted = true;
        project.DeletedAtUtc = DateTime.UtcNow;



        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static ProjectResponse MapToResponse(Project project)
    {

        return new ProjectResponse
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Status = project.Status,
            OwnerId = project.OwnerId,
            CreatedAtUtc = project.CreatedAtUtc,
            RowVersion = Convert.ToBase64String(project.RowVersion)
        };
    }
}
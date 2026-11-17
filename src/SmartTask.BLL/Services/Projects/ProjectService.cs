using FluentValidation;
using SmartTask.BLL.DTOs.Projects;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.BLL.Interfaces.Services;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;
using SmartTask.Common.Constants;
using SmartTask.Common.Exceptions;
using SmartTask.Common.Utilities;

namespace SmartTask.BLL.Services.Projects;

public class ProjectService : IProjectService
{
    private const string AdminRole = RoleNames.Admin;
    private const string ProjectManagerRole = RoleNames.ProjectManager;

    private readonly IProjectRepository _projectRepository;
    private readonly IProjectMemberRepository _memberRepository;
    private readonly IUserAccountRepository _userRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ICurrentRequestContext _requestContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateProjectRequest> _createValidator;
    private readonly IValidator<UpdateProjectRequest> _updateValidator;
    private readonly IValidator<ProjectListQueryRequest> _projectListValidator;

    public ProjectService(
        IProjectRepository projectRepository,
        IProjectMemberRepository memberRepository,
        IUserAccountRepository userRepository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        IValidator<CreateProjectRequest> createValidator,
        IValidator<UpdateProjectRequest> updateValidator,
        IValidator<ProjectListQueryRequest> projectListValidator,
        ICurrentRequestContext requestContext)
    {
        _projectRepository = projectRepository;
        _memberRepository = memberRepository;
        _userRepository = userRepository;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _projectListValidator = projectListValidator;
        _requestContext = requestContext;
    }

    public async Task<ProjectResponse> CreateAsync(
        CreateProjectRequest request,
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(ownerId, nameof(ownerId));
        await EnsureCanCreateProjectAsync(ownerId, cancellationToken);

        ArgumentNullException.ThrowIfNull(request);

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
            CreatedByUserId = ownerId,
            Status = ProjectStatus.Planning
        };

        var managerMembership = new ProjectMember
        {
            ProjectId = project.Id,
            UserId = ownerId,
            ProjectRole = ProjectMemberRole.Manager,
            Project = project
        };

        _projectRepository.Add(project);
        _memberRepository.Add(managerMembership);

        AddAuditLog(ownerId, "ProjectCreated", project.Id, new
        {
            Status = project.Status.ToString()
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(project);
    }

    public async Task<IReadOnlyList<ProjectResponse>> GetMyProjectsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(userId, nameof(userId));

        var roles = await _userRepository.GetRolesAsync(
            userId,
            cancellationToken);

        var projects = roles.Contains(AdminRole, StringComparer.OrdinalIgnoreCase)
            ? await _projectRepository.GetAllAsync(cancellationToken)
            : await _projectRepository.GetByMemberUserIdAsync(
                userId,
                cancellationToken);

        return projects.Select(MapToResponse).ToList();
    }

    public async Task<ProjectListResponse> GetPagedMyProjectsAsync(
        string userId,
        ProjectListQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(userId, nameof(userId));
        ArgumentNullException.ThrowIfNull(query);

        var validationResult = await _projectListValidator.ValidateAsync(
            query,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var roles = await _userRepository.GetRolesAsync(
            userId,
            cancellationToken);

        var isAdmin = roles.Contains(
            AdminRole,
            StringComparer.OrdinalIgnoreCase);

        var (projects, totalCount) = isAdmin
            ? await _projectRepository.GetPagedAllAsync(query, cancellationToken)
            : await _projectRepository.GetPagedByMemberUserIdAsync(
                userId,
                query,
                cancellationToken);

        return new ProjectListResponse
        {
            Items = projects.Select(MapToResponse).ToList(),
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalCount = totalCount,
            TotalPages = Pagination.CalculateTotalPages(
                totalCount,
                query.PageSize)
        };
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

        var roles = await _userRepository.GetRolesAsync(
            userId,
            cancellationToken);

        if (roles.Contains(AdminRole, StringComparer.OrdinalIgnoreCase))
        {
            return MapToResponse(project);
        }

        var membership = await _memberRepository.GetByProjectAndUserAsync(
            projectId,
            userId,
            cancellationToken);

        return membership is null ? null : MapToResponse(project);
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

        if (project is null)
        {
            return null;
        }

        await EnsureCanManageProjectAsync(
            projectId,
            userId,
            cancellationToken);

        ArgumentNullException.ThrowIfNull(request);

        var validationResult = await _updateValidator.ValidateAsync(
            request,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var originalRowVersion = Convert.FromBase64String(request.RowVersion);

        _projectRepository.SetOriginalRowVersion(
            project,
            originalRowVersion);

        var previousStatus = project.Status;
        var newName = request.Name.Trim();
        var newDescription = request.Description?.Trim();

        var changedFields = new (string Field, bool Changed)[]
            {
                (nameof(Project.Name), project.Name != newName),
                (nameof(Project.Description), project.Description != newDescription),
                (nameof(Project.Status),
                    request.Status.HasValue && request.Status.Value != previousStatus)
            }
            .Where(change => change.Changed)
            .Select(change => change.Field)
            .ToList();

        project.Name = newName;
        project.Description = newDescription;

        if (request.Status.HasValue)
        {
            project.Status = request.Status.Value;
        }

        AddAuditLog(userId, "ProjectUpdated", project.Id, new
        {
            ChangedFields = changedFields
        });

        if (project.Status != previousStatus)
        {
            AddAuditLog(userId, "ProjectStatusChanged", project.Id, new
            {
                PreviousStatus = previousStatus.ToString(),
                NewStatus = project.Status.ToString()
            });
        }

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

        if (project is null)
        {
            return false;
        }

        await EnsureCanManageProjectAsync(
            projectId,
            userId,
            cancellationToken);

        project.IsDeleted = true;
        project.DeletedAtUtc = DateTime.UtcNow;

        AddAuditLog(userId, "ProjectDeleted", project.Id, new { });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<ProjectMemberResponse>?> GetMembersAsync(
        Guid projectId,
        string requesterUserId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(requesterUserId))
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

        await EnsureCanManageProjectAsync(
            projectId,
            requesterUserId,
            cancellationToken);

        var memberships = await _memberRepository.GetByProjectIdAsync(
            projectId,
            cancellationToken);

        var responses = new List<ProjectMemberResponse>();

        foreach (var membership in memberships)
        {
            var user = await _userRepository.FindByIdAsync(
                membership.UserId,
                cancellationToken);

            if (user is null)
            {
                continue;
            }

            responses.Add(new ProjectMemberResponse
            {
                UserId = user.Id,
                DisplayName = user.DisplayName,
                Email = user.Email,
                ProjectRole = membership.ProjectRole
            });
        }

        return responses;
    }

    public async Task<ProjectMemberResponse?> AddMemberAsync(
        Guid projectId,
        AddProjectMemberRequest request,
        string requesterUserId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(requesterUserId))
        {
            return null;
        }

        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            throw new ArgumentException("Member user ID is required.");
        }

        var project = await _projectRepository.GetByIdAsync(
            projectId,
            cancellationToken);

        if (project is null)
        {
            return null;
        }

        await EnsureCanManageProjectAsync(
            projectId,
            requesterUserId,
            cancellationToken);

        var user = await _userRepository.FindByIdAsync(
            request.UserId,
            cancellationToken);

        if (user is null)
        {
            return null;
        }

        var existingMembership = await _memberRepository.GetByProjectAndUserAsync(
            projectId,
            user.Id,
            cancellationToken);

        if (existingMembership is not null)
        {
            throw new ArgumentException(
                "The user is already a member of this project.");
        }

        var membership = new ProjectMember
        {
            ProjectId = projectId,
            UserId = user.Id,
            ProjectRole = ProjectMemberRole.Member
        };

        _memberRepository.Add(membership);

        AddAuditLog(requesterUserId, "ProjectMemberAdded", projectId, new
        {
            MemberUserId = user.Id,
            ProjectRole = membership.ProjectRole.ToString()
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ProjectMemberResponse
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
            ProjectRole = membership.ProjectRole
        };
    }

    public async Task<bool> RemoveMemberAsync(
        Guid projectId,
        string memberUserId,
        string requesterUserId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty ||
            string.IsNullOrWhiteSpace(memberUserId) ||
            string.IsNullOrWhiteSpace(requesterUserId))
        {
            return false;
        }

        var project = await _projectRepository.GetByIdAsync(
            projectId,
            cancellationToken);

        if (project is null)
        {
            return false;
        }

        await EnsureCanManageProjectAsync(
            projectId,
            requesterUserId,
            cancellationToken);

        var membership = await _memberRepository.GetByProjectAndUserAsync(
            projectId,
            memberUserId,
            cancellationToken);

        if (membership is null)
        {
            return false;
        }

        _memberRepository.Remove(membership);

        AddAuditLog(requesterUserId, "ProjectMemberRemoved", projectId, new
        {
            MemberUserId = memberUserId
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    private void AddAuditLog(
        string userId,
        string action,
        Guid projectId,
        object details)
    {
        _auditLogRepository.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = "Project",
            EntityId = projectId.ToString(),
            Details = System.Text.Json.JsonSerializer.Serialize(details),
            IpAddress = _requestContext.IpAddress
        });
    }

    private async Task EnsureCanCreateProjectAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var roles = await _userRepository.GetRolesAsync(
            userId,
            cancellationToken);

        if (!roles.Contains(AdminRole, StringComparer.OrdinalIgnoreCase) &&
            !roles.Contains(ProjectManagerRole, StringComparer.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException(
                "Only Admins and Project Managers can create projects.");
        }
    }

    private async Task EnsureCanManageProjectAsync(
        Guid projectId,
        string userId,
        CancellationToken cancellationToken)
    {
        var roles = await _userRepository.GetRolesAsync(
            userId,
            cancellationToken);

        if (roles.Contains(AdminRole, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        if (!roles.Contains(
                ProjectManagerRole,
                StringComparer.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException(
                "Your global role does not permit project management.");
        }

        var membership = await _memberRepository.GetByProjectAndUserAsync(
            projectId,
            userId,
            cancellationToken);

        if (membership?.ProjectRole != ProjectMemberRole.Manager)
        {
            throw new ForbiddenAccessException(
                "You must be a Manager of this project to manage it.");
        }
    }

    private static void EnsureUserId(string userId, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException(
                "User ID is required.",
                parameterName);
        }
    }

    private static ProjectResponse MapToResponse(Project project)
    {
        return new ProjectResponse
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Status = project.Status,
            CreatedByUserId = project.CreatedByUserId,
            CreatedAtUtc = project.CreatedAtUtc,
            RowVersion = Convert.ToBase64String(project.RowVersion)
        };
    }
}

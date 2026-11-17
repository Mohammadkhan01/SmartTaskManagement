using FluentValidation;
using SmartTask.BLL.DTOs.Tasks;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.BLL.Interfaces.Services;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.BLL.Services.Tasks;

public class TaskService : ITaskService
{
    private readonly ITaskRepository _taskRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectMemberRepository _memberRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateTaskRequest> _createValidator;
    private readonly IValidator<UpdateTaskRequest> _updateValidator;
    private readonly IValidator<UpdateTaskStatusRequest> _statusValidator;
    private readonly IValidator<AssignTaskRequest> _assignValidator;
    private readonly IValidator<TaskListQueryRequest> _listQueryValidator;

    public TaskService(
        ITaskRepository taskRepository,
        IProjectRepository projectRepository,
        IProjectMemberRepository memberRepository,
        IUserAccountRepository userAccountRepository,
        INotificationRepository notificationRepository,
        IUnitOfWork unitOfWork,
        IValidator<CreateTaskRequest> createValidator,
        IValidator<UpdateTaskRequest> updateValidator,
        IValidator<UpdateTaskStatusRequest> statusValidator,
        IValidator<AssignTaskRequest> assignValidator,
        IValidator<TaskListQueryRequest> listQueryValidator)
    {
        _taskRepository = taskRepository;
        _projectRepository = projectRepository;
        _memberRepository = memberRepository;
        _userAccountRepository = userAccountRepository;
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _statusValidator = statusValidator;
        _assignValidator = assignValidator;
        _listQueryValidator = listQueryValidator;
    }

    public async Task<IReadOnlyList<TaskResponse>?> GetByProjectIdAsync(
        Guid projectId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        if (!await CanViewTasksAsync(projectId, userId, cancellationToken))
        {
            return null;
        }

        var tasks = await _taskRepository.GetByProjectIdAsync(
            projectId,
            cancellationToken);

        return tasks.Select(MapToResponse).ToList();
    }

    public async Task<TaskListResponse?> GetPagedByProjectIdAsync(
        Guid projectId,
        TaskListQueryRequest query,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
            return null;

        if (!await CanViewTasksAsync(projectId, userId, cancellationToken))
            return null;

        var validationResult = await _listQueryValidator.ValidateAsync(
            query, cancellationToken);

        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var (items, totalCount) =
            await _taskRepository.GetPagedByProjectIdAsync(
                projectId, query, cancellationToken);

        var totalPages = (int)(
            ((long)totalCount + query.PageSize - 1) / query.PageSize);

        return new TaskListResponse
        {
            Items = items.Select(MapToResponse).ToList(),
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasPreviousPage = query.PageNumber > 1,
            HasNextPage = query.PageNumber < totalPages
        };
    }

    public async Task<TaskResponse?> GetByIdAsync(
        Guid taskId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (taskId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var task = await _taskRepository.GetByIdAsync(
            taskId,
            cancellationToken);

        if (task is null)
        {
            return null;
        }

        if (!await CanViewTasksAsync(
                task.ProjectId,
                userId,
                cancellationToken))
        {
            return null;
        }

        return MapToResponse(task);
    }

    public async Task<TaskResponse?> CreateAsync(
        Guid projectId,
        CreateTaskRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        if (!await CanManageTasksAsync(projectId, userId, cancellationToken))
        {
            return null;
        }

        var validationResult = await _createValidator.ValidateAsync(
            request,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var assignedToUserId = request.AssignedToUserId?.Trim();

        if (assignedToUserId is not null)
        {
            await ValidateAssigneeAsync(
                projectId,
                assignedToUserId,
                cancellationToken);
        }

        var task = new TaskItem
        {
            ProjectId = projectId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Priority = request.Priority,
            AssignedToUserId = assignedToUserId,
            CreatedByUserId = userId,
            DueDateUtc = request.DueDateUtc,
            Status = SmartTask.Domain.Enums.TaskStatus.ToDo
        };

        _taskRepository.Add(task);

        if (assignedToUserId is not null && assignedToUserId != userId)
        {
            _notificationRepository.Add(new Notification
            {
                RecipientUserId = assignedToUserId,
                Type = "TaskAssigned",
                Title = "Task assigned to you",
                Message = $"You have been assigned the task '{task.Title}'.",
                RelatedEntityId = task.Id,
                RelatedEntityType = "Task"
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(task);
    }

    public async Task<TaskResponse?> UpdateAsync(
        Guid taskId,
        UpdateTaskRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (taskId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var task = await _taskRepository.GetByIdAsync(
            taskId,
            cancellationToken);

        if (task is null)
        {
            return null;
        }

        if (!await CanManageTasksAsync(
                task.ProjectId,
                userId,
                cancellationToken))
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

        var assignedToUserId = request.AssignedToUserId?.Trim();
        var previousAssigneeId = task.AssignedToUserId;

        if (assignedToUserId is not null)
        {
            await ValidateAssigneeAsync(
                task.ProjectId,
                assignedToUserId,
                cancellationToken);
        }

        _taskRepository.SetOriginalRowVersion(
            task,
            Convert.FromBase64String(request.RowVersion));

        task.Title = request.Title.Trim();
        task.Description = request.Description?.Trim();
        task.Priority = request.Priority;
        task.AssignedToUserId = assignedToUserId;
        task.DueDateUtc = request.DueDateUtc;

        if (assignedToUserId is not null &&
            assignedToUserId != previousAssigneeId &&
            assignedToUserId != userId)
        {
            _notificationRepository.Add(new Notification
            {
                RecipientUserId = assignedToUserId,
                Type = previousAssigneeId is null
                    ? "TaskAssigned"
                    : "TaskReassigned",
                Title = "Task assigned to you",
                Message = $"You have been assigned the task '{task.Title}'.",
                RelatedEntityId = task.Id,
                RelatedEntityType = "Task"
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(task);
    }

    public async Task<TaskResponse?> AssignAsync(
        Guid taskId,
        AssignTaskRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (taskId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
            return null;

        var validationResult = await _assignValidator.ValidateAsync(
            request,
            cancellationToken);

        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var task = await _taskRepository.GetByIdAsync(
            taskId,
            cancellationToken);

        if (task is null || task.IsDeleted)
            return null;

        var project = await _projectRepository.GetByIdAsync(
            task.ProjectId,
            cancellationToken);

        if (project is null ||
            project.IsDeleted ||
            project.Status != ProjectStatus.Active)
        {
            return null;
        }

        if (!await CanManageTasksAsync(
                task.ProjectId,
                userId,
                cancellationToken))
        {
            return null;
        }

        var assignedToUserId = request.AssignedToUserId.Trim();

        await ValidateAssigneeAsync(
            task.ProjectId,
            assignedToUserId,
            cancellationToken);

        var previousAssigneeId = task.AssignedToUserId;

        _taskRepository.SetOriginalRowVersion(
            task,
            Convert.FromBase64String(request.RowVersion));

        task.AssignedToUserId = assignedToUserId;

        if (assignedToUserId != previousAssigneeId &&
            assignedToUserId != userId)
        {
            _notificationRepository.Add(new Notification
            {
                RecipientUserId = assignedToUserId,
                Type = previousAssigneeId is null
                    ? "TaskAssigned"
                    : "TaskReassigned",
                Title = "Task assigned to you",
                Message = $"You have been assigned the task '{task.Title}'.",
                RelatedEntityId = task.Id,
                RelatedEntityType = "Task"
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(task);
    }

    public async Task<TaskResponse?> UpdateStatusAsync(
        Guid taskId,
        UpdateTaskStatusRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (taskId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var task = await _taskRepository.GetByIdAsync(
            taskId,
            cancellationToken);

        if (task is null)
        {
            return null;
        }

        if (!await CanUpdateTaskStatusAsync(
                task,
                userId,
                cancellationToken))
        {
            return null;
        }

        var validationResult = await _statusValidator.ValidateAsync(
            request,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var previousStatus = task.Status;

        _taskRepository.SetOriginalRowVersion(
            task,
            Convert.FromBase64String(request.RowVersion));

        task.Status = request.Status;

        if (task.Status != previousStatus &&
            task.CreatedByUserId != userId)
        {
            _notificationRepository.Add(new Notification
            {
                RecipientUserId = task.CreatedByUserId,
                Type = "TaskStatusChanged",
                Title = "Task status updated",
                Message = $"The status of '{task.Title}' changed to {task.Status}.",
                RelatedEntityId = task.Id,
                RelatedEntityType = "Task"
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(task);
    }

    public async Task<bool> DeleteAsync(
        Guid taskId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (taskId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var task = await _taskRepository.GetByIdAsync(
            taskId,
            cancellationToken);

        if (task is null)
        {
            return false;
        }

        if (!await CanManageTasksAsync(
                task.ProjectId,
                userId,
                cancellationToken))
        {
            return false;
        }

        task.IsDeleted = true;
        task.DeletedAtUtc = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<bool> CanViewTasksAsync(
        Guid projectId,
        string userId,
        CancellationToken cancellationToken)
    {
        var roles = await _userAccountRepository.GetRolesAsync(
            userId,
            cancellationToken);

        if (roles.Contains("Admin", StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        var hasPermittedGlobalRole =
            roles.Contains("Project Manager", StringComparer.OrdinalIgnoreCase) ||
            roles.Contains("Team Member", StringComparer.OrdinalIgnoreCase);

        if (!hasPermittedGlobalRole)
        {
            return false;
        }

        return await _memberRepository.GetByProjectAndUserAsync(
            projectId,
            userId,
            cancellationToken) is not null;
    }

    private async Task<bool> CanManageTasksAsync(
        Guid projectId,
        string userId,
        CancellationToken cancellationToken)
    {
        var roles = await _userAccountRepository.GetRolesAsync(
            userId,
            cancellationToken);

        if (roles.Contains("Admin", StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!roles.Contains("Project Manager", StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var membership = await _memberRepository.GetByProjectAndUserAsync(
            projectId,
            userId,
            cancellationToken);

        return membership?.ProjectRole == ProjectMemberRole.Manager;
    }

    private async Task<bool> CanUpdateTaskStatusAsync(
        TaskItem task,
        string userId,
        CancellationToken cancellationToken)
    {
        var roles = await _userAccountRepository.GetRolesAsync(
            userId,
            cancellationToken);

        if (roles.Contains("Admin", StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        var membership = await _memberRepository.GetByProjectAndUserAsync(
            task.ProjectId,
            userId,
            cancellationToken);

        if (membership is null)
        {
            return false;
        }

        if (roles.Contains("Project Manager", StringComparer.OrdinalIgnoreCase) &&
            membership.ProjectRole == ProjectMemberRole.Manager)
        {
            return true;
        }

        return roles.Contains("Team Member", StringComparer.OrdinalIgnoreCase) &&
               task.AssignedToUserId == userId;
    }

    private async Task ValidateAssigneeAsync(
        Guid projectId,
        string assignedToUserId,
        CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(
            projectId,
            cancellationToken);

        if (project is null ||
            project.IsDeleted ||
            project.Status != ProjectStatus.Active)
        {
            throw new ValidationException(
                "Tasks can only be assigned within an active project.");
        }

        var account = await _userAccountRepository.FindByIdAsync(
            assignedToUserId,
            cancellationToken);

        if (account is null || !account.IsActive)
        {
            throw new ValidationException(
                "The assigned user must exist and have an active account.");
        }

        var membership = await _memberRepository.GetByProjectAndUserAsync(
            projectId,
            assignedToUserId,
            cancellationToken);

        if (membership is null)
        {
            throw new ValidationException(
                "The assigned user must be a member of this project.");
        }
    }

    private static TaskResponse MapToResponse(TaskItem task)
    {
        return new TaskResponse
        {
            Id = task.Id,
            ProjectId = task.ProjectId,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            Priority = task.Priority,
            AssignedToUserId = task.AssignedToUserId,
            CreatedByUserId = task.CreatedByUserId,
            DueDateUtc = task.DueDateUtc,
            CreatedAtUtc = task.CreatedAtUtc,
            UpdatedAtUtc = task.UpdatedAtUtc,
            RowVersion = Convert.ToBase64String(task.RowVersion)
        };
    }
}

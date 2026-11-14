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
    private readonly IProjectMemberRepository _memberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateTaskRequest> _createValidator;
    private readonly IValidator<UpdateTaskRequest> _updateValidator;
    private readonly IValidator<UpdateTaskStatusRequest> _statusValidator;

    public TaskService(
        ITaskRepository taskRepository,
        IProjectMemberRepository memberRepository,
        IUnitOfWork unitOfWork,
        IValidator<CreateTaskRequest> createValidator,
        IValidator<UpdateTaskRequest> updateValidator,
        IValidator<UpdateTaskStatusRequest> statusValidator)
    {
        _taskRepository = taskRepository;
        _memberRepository = memberRepository;
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _statusValidator = statusValidator;
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

        var membership = await _memberRepository.GetByProjectAndUserAsync(
            projectId,
            userId,
            cancellationToken);

        if (membership is null)
        {
            return null;
        }

        var tasks = await _taskRepository.GetByProjectIdAsync(
            projectId,
            cancellationToken);

        return tasks.Select(MapToResponse).ToList();
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

        var membership = await _memberRepository.GetByProjectAndUserAsync(
            task.ProjectId,
            userId,
            cancellationToken);

        if (membership is null)
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

        var membership = await _memberRepository.GetByProjectAndUserAsync(
            projectId,
            userId,
            cancellationToken);

        if (membership is null || membership.Role != ProjectMemberRole.Manager)
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
            var assigneeMembership =
                await _memberRepository.GetByProjectAndUserAsync(
                    projectId,
                    assignedToUserId,
                    cancellationToken);

            if (assigneeMembership is null)
            {
                throw new ValidationException(
                    "The assigned user must be a member of this project.");
            }
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

        var membership = await _memberRepository.GetByProjectAndUserAsync(
            task.ProjectId,
            userId,
            cancellationToken);

        if (membership is null || membership.Role != ProjectMemberRole.Manager)
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

        if (assignedToUserId is not null)
        {
            var assigneeMembership =
                await _memberRepository.GetByProjectAndUserAsync(
                    task.ProjectId,
                    assignedToUserId,
                    cancellationToken);

            if (assigneeMembership is null)
            {
                throw new ValidationException(
                    "The assigned user must be a member of this project.");
            }
        }

        _taskRepository.SetOriginalRowVersion(
            task,
            Convert.FromBase64String(request.RowVersion));

        task.Title = request.Title.Trim();
        task.Description = request.Description?.Trim();
        task.Priority = request.Priority;
        task.AssignedToUserId = assignedToUserId;
        task.DueDateUtc = request.DueDateUtc;

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

        var membership = await _memberRepository.GetByProjectAndUserAsync(
            task.ProjectId,
            userId,
            cancellationToken);

        if (membership is null)
        {
            return null;
        }

        var isManager = membership.Role == ProjectMemberRole.Manager;
        var isAssignedMember = task.AssignedToUserId == userId;

        if (!isManager && !isAssignedMember)
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

        _taskRepository.SetOriginalRowVersion(
            task,
            Convert.FromBase64String(request.RowVersion));

        task.Status = request.Status;

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

        var membership = await _memberRepository.GetByProjectAndUserAsync(
            task.ProjectId,
            userId,
            cancellationToken);

        if (membership is null || membership.Role != ProjectMemberRole.Manager)
        {
            return false;
        }

        task.IsDeleted = true;
        task.DeletedAtUtc = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
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

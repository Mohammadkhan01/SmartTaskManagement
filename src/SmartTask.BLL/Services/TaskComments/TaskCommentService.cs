using FluentValidation;
using SmartTask.BLL.DTOs.TaskComments;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.BLL.Interfaces.Services;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.BLL.Services.TaskComments;

public class TaskCommentService : ITaskCommentService
{
    private readonly ITaskCommentRepository _commentRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IProjectMemberRepository _memberRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateTaskCommentRequest> _createValidator;
    private readonly IValidator<UpdateTaskCommentRequest> _updateValidator;

    public TaskCommentService(
        ITaskCommentRepository commentRepository,
        ITaskRepository taskRepository,
        IAuditLogRepository auditLogRepository,
        IProjectMemberRepository memberRepository,
        INotificationRepository notificationRepository,
        IUnitOfWork unitOfWork,
        IValidator<CreateTaskCommentRequest> createValidator,
        IValidator<UpdateTaskCommentRequest> updateValidator)
    {
        _commentRepository = commentRepository;
        _taskRepository = taskRepository;
        _auditLogRepository = auditLogRepository;
        _memberRepository = memberRepository;
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<TaskCommentResponse>?> GetByTaskIdAsync(
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

        var comments = await _commentRepository.GetByTaskIdAsync(
            taskId,
            cancellationToken);

        return comments.Select(MapToResponse).ToList();
    }

    public async Task<TaskCommentResponse?> CreateAsync(
        Guid taskId,
        CreateTaskCommentRequest request,
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

        var validationResult = await _createValidator.ValidateAsync(
            request,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var comment = new TaskComment
        {
            TaskId = taskId,
            UserId = userId,
            Content = request.Content.Trim()
        };

        _commentRepository.Add(comment);
        AddActivity(taskId, userId, "CommentCreated", comment.Id);

        if (task.AssignedToUserId is not null &&
            task.AssignedToUserId != userId)
        {
            _notificationRepository.Add(new Notification
            {
                RecipientUserId = task.AssignedToUserId,
                Type = "TaskCommentAdded",
                Title = "New comment on your task",
                Message = $"A new comment was added to '{task.Title}'.",
                RelatedEntityId = task.Id,
                RelatedEntityType = "Task"
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(comment);
    }

    public async Task<TaskCommentResponse?> UpdateAsync(
        Guid commentId,
        UpdateTaskCommentRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (commentId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var comment = await _commentRepository.GetByIdAsync(
            commentId,
            cancellationToken);

        if (comment is null || comment.UserId != userId)
        {
            return null;
        }

        var task = await _taskRepository.GetByIdAsync(
            comment.TaskId,
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

        var validationResult = await _updateValidator.ValidateAsync(
            request,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        _commentRepository.SetOriginalRowVersion(
            comment,
            Convert.FromBase64String(request.RowVersion));

        comment.Content = request.Content.Trim();
        AddActivity(comment.TaskId, userId, "CommentUpdated", comment.Id);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(comment);
    }

    public async Task<bool> DeleteAsync(
        Guid commentId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (commentId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var comment = await _commentRepository.GetByIdAsync(
            commentId,
            cancellationToken);

        if (comment is null)
        {
            return false;
        }

        var task = await _taskRepository.GetByIdAsync(
            comment.TaskId,
            cancellationToken);

        if (task is null)
        {
            return false;
        }

        var membership = await _memberRepository.GetByProjectAndUserAsync(
            task.ProjectId,
            userId,
            cancellationToken);

        if (membership is null)
        {
            return false;
        }

        var isAuthor = comment.UserId == userId;
        var isManager = membership.Role == ProjectMemberRole.Manager;

        if (!isAuthor && !isManager)
        {
            return false;
        }

        comment.IsDeleted = true;
        comment.DeletedAtUtc = DateTime.UtcNow;
        AddActivity(comment.TaskId, userId, "CommentDeleted", comment.Id);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    private void AddActivity(
        Guid taskId,
        string userId,
        string action,
        Guid commentId)
    {
        _auditLogRepository.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = "Task",
            EntityId = taskId.ToString(),
            Details = System.Text.Json.JsonSerializer.Serialize(
                new { CommentId = commentId })
        });
    }

    private static TaskCommentResponse MapToResponse(TaskComment comment)
    {
        return new TaskCommentResponse
        {
            Id = comment.Id,
            TaskId = comment.TaskId,
            UserId = comment.UserId,
            Content = comment.Content,
            CreatedAtUtc = comment.CreatedAtUtc,
            UpdatedAtUtc = comment.UpdatedAtUtc,
            RowVersion = Convert.ToBase64String(comment.RowVersion)
        };
    }
}

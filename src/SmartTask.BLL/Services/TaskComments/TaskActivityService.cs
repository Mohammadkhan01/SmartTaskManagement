using SmartTask.BLL.DTOs.TaskComments;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.BLL.Interfaces.Services;

namespace SmartTask.BLL.Services.TaskComments;

public sealed class TaskActivityService : ITaskActivityService
{
    private readonly ITaskRepository _taskRepository;
    private readonly IProjectMemberRepository _memberRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public TaskActivityService(
        ITaskRepository taskRepository,
        IProjectMemberRepository memberRepository,
        IAuditLogRepository auditLogRepository)
    {
        _taskRepository = taskRepository;
        _memberRepository = memberRepository;
        _auditLogRepository = auditLogRepository;
    }

    public async Task<IReadOnlyList<TaskActivityResponse>?> GetByTaskIdAsync(
        Guid taskId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (taskId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
            return null;

        var task = await _taskRepository.GetByIdAsync(
            taskId,
            cancellationToken);

        if (task is null)
            return null;

        var membership = await _memberRepository.GetByProjectAndUserAsync(
            task.ProjectId,
            userId,
            cancellationToken);

        if (membership is null)
            return null;

        var activityLogs = await _auditLogRepository.GetByTaskIdAsync(
            taskId,
            cancellationToken);

        return activityLogs
            .Select(activity => new TaskActivityResponse
            {
                Id = activity.Id,
                UserId = activity.UserId,
                Action = activity.Action,
                EntityName = activity.EntityName,
                EntityId = activity.EntityId,
                Details = activity.Details,
                TimestampUtc = activity.TimestampUtc
            })
            .ToList();
    }
}

using FluentValidation;
using SmartTask.BLL.DTOs.TaskComments;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.BLL.Services.TaskComments;
using SmartTask.BLL.Validators;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.UnitTests;

public class TaskCommentServiceTests
{
    private readonly FakeTaskCommentRepository _comments = new();
    private readonly FakeTaskRepository _tasks = new();
    private readonly FakeAuditLogRepository _auditLogs = new();
    private readonly FakeProjectMemberRepository _members = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private TaskCommentService CreateService()
    {
        return new TaskCommentService(
            _comments,
            _tasks,
            _auditLogs,
            _members,
            _notifications,
            _unitOfWork,
            new CreateTaskCommentRequestValidator(),
            new UpdateTaskCommentRequestValidator(),
            new FakeCurrentRequestContext());
    }

    private TaskItem CreateTask(
        Guid? projectId = null,
        string? assignedToUserId = null)
    {
        var task = new TaskItem
        {
            ProjectId = projectId ?? Guid.NewGuid(),
            Title = "Test task",
            CreatedByUserId = "manager-123",
            AssignedToUserId = assignedToUserId
        };

        _tasks.AddedTasks.Add(task);
        return task;
    }

    private void AddMember(
        Guid projectId,
        string userId,
        ProjectMemberRole role = ProjectMemberRole.Member)
    {
        _members.AddedMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            ProjectRole = role
        });
    }

    private TaskComment CreateComment(
        Guid taskId,
        string userId = "author-123")
    {
        var comment = new TaskComment
        {
            TaskId = taskId,
            UserId = userId,
            Content = "Original comment",
            RowVersion = [1, 2, 3, 4, 5, 6, 7, 8]
        };

        _comments.AddedComments.Add(comment);
        return comment;
    }

    [Fact]
    public async Task GetByTaskIdAsync_ShouldReturnCommentsForProjectMember()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "member-123");
        var comment = CreateComment(task.Id);

        var result = await CreateService().GetByTaskIdAsync(
            task.Id, "member-123");

        var response = Assert.Single(result!);
        Assert.Equal(comment.Id, response.Id);
        Assert.Equal("Original comment", response.Content);
    }

    [Fact]
    public async Task GetByTaskIdAsync_ShouldRejectNonMember()
    {
        var task = CreateTask();
        CreateComment(task.Id);

        var result = await CreateService().GetByTaskIdAsync(
            task.Id, "outsider-123");

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateCommentForProjectMember()
    {
        var task = CreateTask(assignedToUserId: "assignee-123");
        AddMember(task.ProjectId, "member-123");

        var result = await CreateService().CreateAsync(
            task.Id,
            new CreateTaskCommentRequest
            {
                Content = "  New comment  "
            },
            "member-123");

        Assert.NotNull(result);
        Assert.Equal(task.Id, result.TaskId);
        Assert.Equal("member-123", result.UserId);
        Assert.Equal("New comment", result.Content);
        Assert.Single(_comments.AddedComments);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);

        var notification = Assert.Single(_notifications.AddedNotifications);
        Assert.Equal("assignee-123", notification.RecipientUserId);
        Assert.Equal("TaskCommentAdded", notification.Type);
        Assert.Equal(task.Id, notification.RelatedEntityId);
        Assert.Equal("Task", notification.RelatedEntityType);
        Assert.DoesNotContain("New comment", notification.Message);

        var activity = Assert.Single(_auditLogs.AddedLogs);
        Assert.Equal("CommentCreated", activity.Action);
        Assert.Equal("Task", activity.EntityName);
        Assert.Equal(task.Id.ToString(), activity.EntityId);
        Assert.Equal("member-123", activity.UserId);
        Assert.Contains(result.Id.ToString(), activity.Details);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectNonMember()
    {
        var task = CreateTask();

        var result = await CreateService().CreateAsync(
            task.Id,
            new CreateTaskCommentRequest { Content = "New comment" },
            "outsider-123");

        Assert.Null(result);
        Assert.Empty(_comments.AddedComments);
        Assert.Empty(_notifications.AddedNotifications);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_ShouldNotNotifyAssigneeWhenTheyComment()
    {
        var task = CreateTask(assignedToUserId: "member-123");
        AddMember(task.ProjectId, "member-123");

        var result = await CreateService().CreateAsync(
            task.Id,
            new CreateTaskCommentRequest { Content = "My own comment" },
            "member-123");

        Assert.NotNull(result);
        Assert.Single(_comments.AddedComments);
        Assert.Empty(_notifications.AddedNotifications);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectInvalidContent()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "member-123");

        await Assert.ThrowsAsync<ValidationException>(
            () => CreateService().CreateAsync(
                task.Id,
                new CreateTaskCommentRequest { Content = "   " },
                "member-123"));

        Assert.Empty(_comments.AddedComments);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateAsync_ShouldAllowAuthorToUpdateComment()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "author-123");
        var comment = CreateComment(task.Id);
        var rowVersion = Convert.ToBase64String(comment.RowVersion);

        var result = await CreateService().UpdateAsync(
            comment.Id,
            new UpdateTaskCommentRequest
            {
                Content = "  Updated comment  ",
                RowVersion = rowVersion
            },
            "author-123");

        Assert.NotNull(result);
        Assert.Equal("Updated comment", result.Content);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        Assert.Equal(comment.RowVersion, _comments.ReceivedOriginalRowVersion);

        var activity = Assert.Single(_auditLogs.AddedLogs);
        Assert.Equal("CommentUpdated", activity.Action);
        Assert.Equal(task.Id.ToString(), activity.EntityId);
        Assert.Equal("author-123", activity.UserId);
        Assert.Contains(comment.Id.ToString(), activity.Details);
    }

    [Fact]
    public async Task UpdateAsync_ShouldRejectAnotherProjectMember()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "another-member");
        var comment = CreateComment(task.Id);
        var originalContent = comment.Content;

        var result = await CreateService().UpdateAsync(
            comment.Id,
            new UpdateTaskCommentRequest
            {
                Content = "Changed comment",
                RowVersion = Convert.ToBase64String(comment.RowVersion)
            },
            "another-member");

        Assert.Null(result);
        Assert.Equal(originalContent, comment.Content);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeleteAsync_ShouldAllowAuthorToDeleteComment()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "author-123");
        var comment = CreateComment(task.Id);

        var result = await CreateService().DeleteAsync(
            comment.Id, "author-123");

        Assert.True(result);
        Assert.True(comment.IsDeleted);
        Assert.NotNull(comment.DeletedAtUtc);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);

        var activity = Assert.Single(_auditLogs.AddedLogs);
        Assert.Equal("CommentDeleted", activity.Action);
        Assert.Equal(task.Id.ToString(), activity.EntityId);
        Assert.Equal("author-123", activity.UserId);
        Assert.Contains(comment.Id.ToString(), activity.Details);
    }

    [Fact]
    public async Task DeleteAsync_ShouldAllowManagerToDeleteAnotherUsersComment()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "manager-123", ProjectMemberRole.Manager);
        var comment = CreateComment(task.Id);

        var result = await CreateService().DeleteAsync(
            comment.Id, "manager-123");

        Assert.True(result);
        Assert.True(comment.IsDeleted);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRejectOtherProjectMember()
    {
        var task = CreateTask();
        AddMember(task.ProjectId, "another-member");
        var comment = CreateComment(task.Id);

        var result = await CreateService().DeleteAsync(
            comment.Id, "another-member");

        Assert.False(result);
        Assert.False(comment.IsDeleted);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }


    private sealed class FakeNotificationRepository : INotificationRepository
    {
        public List<Notification> AddedNotifications { get; } = [];

        public void Add(Notification notification)
        {
            AddedNotifications.Add(notification);
        }

        public Task<Notification?> GetByIdForRecipientAsync(
            Guid notificationId,
            string recipientUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(AddedNotifications.FirstOrDefault(
                n => n.Id == notificationId &&
                     n.RecipientUserId == recipientUserId));
        }

        public Task<IReadOnlyList<Notification>> GetPagedByRecipientAsync(
            string recipientUserId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Notification> result = AddedNotifications
                .Where(n => n.RecipientUserId == recipientUserId)
                .OrderByDescending(n => n.CreatedAtUtc)
                .ThenByDescending(n => n.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult(result);
        }

        public Task<int> CountByRecipientAsync(
            string recipientUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(AddedNotifications.Count(
                n => n.RecipientUserId == recipientUserId));
        }

        public Task<int> CountUnreadByRecipientAsync(
            string recipientUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(AddedNotifications.Count(
                n => n.RecipientUserId == recipientUserId && !n.IsRead));
        }

        public Task<int> MarkAllAsReadAsync(
            string recipientUserId,
            DateTime readAtUtc,
            CancellationToken cancellationToken = default)
        {
            var notifications = AddedNotifications
                .Where(n => n.RecipientUserId == recipientUserId && !n.IsRead)
                .ToList();

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
                notification.ReadAtUtc = readAtUtc;
                notification.UpdatedAtUtc = readAtUtc;
            }

            return Task.FromResult(notifications.Count);
        }
    }

    private sealed class FakeTaskCommentRepository : ITaskCommentRepository
    {
        public List<TaskComment> AddedComments { get; } = [];

        public byte[]? ReceivedOriginalRowVersion { get; private set; }

        public Task<TaskComment?> GetByIdAsync(
            Guid commentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                AddedComments.FirstOrDefault(c => c.Id == commentId && !c.IsDeleted));
        }

        public Task<IReadOnlyList<TaskComment>> GetByTaskIdAsync(
            Guid taskId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<TaskComment> result = AddedComments
                .Where(c => c.TaskId == taskId && !c.IsDeleted)
                .OrderBy(c => c.CreatedAtUtc)
                .ToList();

            return Task.FromResult(result);
        }

        public void SetOriginalRowVersion(TaskComment comment, byte[] rowVersion)
        {
            ReceivedOriginalRowVersion = rowVersion;
        }

        public void Add(TaskComment comment)
        {
            AddedComments.Add(comment);
        }
    }

    private sealed class FakeAuditLogRepository : IAuditLogRepository
    {
        public List<AuditLog> AddedLogs { get; } = [];

        public Task<IReadOnlyList<AuditLog>> GetByTaskIdAsync(
            Guid taskId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AuditLog> result = AddedLogs
                .Where(a => a.EntityName == "Task" &&
                            a.EntityId == taskId.ToString())
                .ToList();

            return Task.FromResult(result);
        }

        public void Add(AuditLog auditLog)
        {
            AddedLogs.Add(auditLog);
        }
    }

    private sealed class FakeTaskRepository : ITaskRepository
    {
        public List<TaskItem> AddedTasks { get; } = [];

        public Task<TaskItem?> GetByIdAsync(
            Guid taskId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                AddedTasks.FirstOrDefault(t => t.Id == taskId && !t.IsDeleted));
        }

        public Task<IReadOnlyList<TaskItem>> GetByProjectIdAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<TaskItem> result = AddedTasks
                .Where(t => t.ProjectId == projectId && !t.IsDeleted)
                .ToList();

            return Task.FromResult(result);
        }

        public async Task<(IReadOnlyList<TaskItem> Items, int TotalCount)>
            GetPagedByProjectIdAsync(
                Guid projectId,
                SmartTask.BLL.DTOs.Tasks.TaskListQueryRequest query,
                CancellationToken cancellationToken = default)
        {
            var filtered = AddedTasks
                .Where(t => t.ProjectId == projectId && !t.IsDeleted)
                .ToList();

            var totalCount = filtered.Count;
            IReadOnlyList<TaskItem> items = filtered
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            return (items, totalCount);
        }

        public void SetOriginalRowVersion(TaskItem task, byte[] rowVersion) { }

        public void Add(TaskItem task) => AddedTasks.Add(task);

        public void Update(TaskItem task) { }
    }

    private sealed class FakeProjectMemberRepository : IProjectMemberRepository
    {
        public List<ProjectMember> AddedMembers { get; } = [];

        public Task<ProjectMember?> GetByProjectAndUserAsync(
            Guid projectId,
            string userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                AddedMembers.FirstOrDefault(
                    m => m.ProjectId == projectId && m.UserId == userId));
        }

        public Task<IReadOnlyList<ProjectMember>> GetByProjectIdAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ProjectMember> result = AddedMembers
                .Where(m => m.ProjectId == projectId)
                .ToList();

            return Task.FromResult(result);
        }

        public void Add(ProjectMember member) => AddedMembers.Add(member);

        public void Remove(ProjectMember member) => AddedMembers.Remove(member);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCallCount { get; private set; }

        public Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            return operation(cancellationToken);
        }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }
    }
}

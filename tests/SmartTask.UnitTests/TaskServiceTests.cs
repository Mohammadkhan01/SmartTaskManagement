using FluentValidation;
using SmartTask.BLL.DTOs.Tasks;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.BLL.Interfaces.Services;
using SmartTask.BLL.Services.Tasks;
using SmartTask.BLL.Validators;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;
using TaskStatus = SmartTask.Domain.Enums.TaskStatus;

namespace SmartTask.UnitTests;

public class TaskServiceTests
{
    private readonly FakeTaskRepository _tasks = new();
    private readonly FakeProjectMemberRepository _members = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private TaskService CreateService()
    {
        return new TaskService(
            _tasks,
            _members,
            _notifications,
            _unitOfWork,
            new CreateTaskRequestValidator(),
            new UpdateTaskRequestValidator(),
            new UpdateTaskStatusRequestValidator());
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
            Role = role
        });
    }

    private TaskItem CreateTask(
        Guid? projectId = null,
        string? assignedToUserId = null)
    {
        var task = new TaskItem
        {
            ProjectId = projectId ?? Guid.NewGuid(),
            Title = "Test task",
            Description = "Test description",
            CreatedByUserId = "manager-123",
            AssignedToUserId = assignedToUserId,
            RowVersion = [1, 2, 3, 4, 5, 6, 7, 8]
        };

        _tasks.AddedTasks.Add(task);
        return task;
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateTaskForManager()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "manager-123", ProjectMemberRole.Manager);
        AddMember(projectId, "member-123");

        var service = CreateService();

        var result = await service.CreateAsync(
            projectId,
            new CreateTaskRequest
            {
                Title = "  New task  ",
                Description = "  Task description  ",
                Priority = TaskPriority.High,
                AssignedToUserId = " member-123 "
            },
            "manager-123");

        Assert.NotNull(result);
        Assert.Equal(projectId, result.ProjectId);
        Assert.Equal("New task", result.Title);
        Assert.Equal("Task description", result.Description);
        Assert.Equal(TaskStatus.ToDo, result.Status);
        Assert.Equal(TaskPriority.High, result.Priority);
        Assert.Equal("member-123", result.AssignedToUserId);
        Assert.Equal("manager-123", result.CreatedByUserId);

        Assert.Single(_tasks.AddedTasks);
        var notification = Assert.Single(_notifications.AddedNotifications);
        Assert.Equal("member-123", notification.RecipientUserId);
        Assert.Equal("TaskAssigned", notification.Type);
        Assert.Equal(result.Id, notification.RelatedEntityId);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectNonManager()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "member-123");

        var service = CreateService();

        var result = await service.CreateAsync(
            projectId,
            new CreateTaskRequest { Title = "New task" },
            "member-123");

        Assert.Null(result);
        Assert.Empty(_tasks.AddedTasks);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectInvalidRequest()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "manager-123", ProjectMemberRole.Manager);

        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(
            () => service.CreateAsync(
                projectId,
                new CreateTaskRequest { Title = "   " },
                "manager-123"));

        Assert.Empty(_tasks.AddedTasks);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectAssigneeWhoIsNotProjectMember()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "manager-123", ProjectMemberRole.Manager);

        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(
            () => service.CreateAsync(
                projectId,
                new CreateTaskRequest
                {
                    Title = "New task",
                    AssignedToUserId = "outsider-123"
                },
                "manager-123"));

        Assert.Empty(_tasks.AddedTasks);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task GetByProjectIdAsync_ShouldReturnTasksForProjectMember()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "member-123");

        var task = CreateTask(projectId);

        var service = CreateService();

        var result = await service.GetByProjectIdAsync(
            projectId,
            "member-123");

        var response = Assert.Single(result!);

        Assert.Equal(task.Id, response.Id);
        Assert.Equal(task.Title, response.Title);
        Assert.Equal(projectId, response.ProjectId);
    }

    [Fact]
    public async Task GetByProjectIdAsync_ShouldRejectNonMember()
    {
        var projectId = Guid.NewGuid();
        CreateTask(projectId);

        var service = CreateService();

        var result = await service.GetByProjectIdAsync(
            projectId,
            "outsider-123");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnTaskForProjectMember()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "member-123");

        var task = CreateTask(projectId);

        var service = CreateService();

        var result = await service.GetByIdAsync(
            task.Id,
            "member-123");

        Assert.NotNull(result);
        Assert.Equal(task.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldRejectNonMember()
    {
        var projectId = Guid.NewGuid();
        var task = CreateTask(projectId);

        var service = CreateService();

        var result = await service.GetByIdAsync(
            task.Id,
            "outsider-123");

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateTaskForManager()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "manager-123", ProjectMemberRole.Manager);

        var task = CreateTask(projectId);
        var rowVersion = Convert.ToBase64String(task.RowVersion);

        var service = CreateService();

        var result = await service.UpdateAsync(
            task.Id,
            new UpdateTaskRequest
            {
                Title = "  Updated task  ",
                Description = "  Updated description  ",
                Priority = TaskPriority.Critical,
                RowVersion = rowVersion
            },
            "manager-123");

        Assert.NotNull(result);
        Assert.Equal("Updated task", result.Title);
        Assert.Equal("Updated description", result.Description);
        Assert.Equal(TaskPriority.Critical, result.Priority);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        Assert.Equal(task.RowVersion, _tasks.ReceivedOriginalRowVersion);
    }

    [Fact]
    public async Task UpdateAsync_ShouldNotifyNewAssigneeOnReassignment()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "manager-123", ProjectMemberRole.Manager);
        AddMember(projectId, "new-member-123");

        var task = CreateTask(projectId, "old-member-123");
        var rowVersion = Convert.ToBase64String(task.RowVersion);

        var result = await CreateService().UpdateAsync(
            task.Id,
            new UpdateTaskRequest
            {
                Title = task.Title,
                Description = task.Description,
                Priority = task.Priority,
                AssignedToUserId = "new-member-123",
                RowVersion = rowVersion
            },
            "manager-123");

        Assert.NotNull(result);
        Assert.Equal("new-member-123", result.AssignedToUserId);

        var notification = Assert.Single(_notifications.AddedNotifications);
        Assert.Equal("new-member-123", notification.RecipientUserId);
        Assert.Equal("TaskReassigned", notification.Type);
        Assert.Equal(task.Id, notification.RelatedEntityId);
        Assert.Equal("Task", notification.RelatedEntityType);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateAsync_ShouldRejectNonManager()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "member-123");

        var task = CreateTask(projectId);
        var originalTitle = task.Title;

        var service = CreateService();

        var result = await service.UpdateAsync(
            task.Id,
            new UpdateTaskRequest
            {
                Title = "Changed task",
                RowVersion = Convert.ToBase64String(task.RowVersion)
            },
            "member-123");

        Assert.Null(result);
        Assert.Equal(originalTitle, task.Title);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldAllowAssignedMember()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "member-123");

        var task = CreateTask(projectId, "member-123");
        var rowVersion = Convert.ToBase64String(task.RowVersion);

        var service = CreateService();

        var result = await service.UpdateStatusAsync(
            task.Id,
            new UpdateTaskStatusRequest
            {
                Status = TaskStatus.InProgress,
                RowVersion = rowVersion
            },
            "member-123");

        Assert.NotNull(result);
        Assert.Equal(TaskStatus.InProgress, result.Status);
        var notification = Assert.Single(_notifications.AddedNotifications);
        Assert.Equal("manager-123", notification.RecipientUserId);
        Assert.Equal("TaskStatusChanged", notification.Type);
        Assert.Equal(task.Id, notification.RelatedEntityId);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldRejectUnassignedMember()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "member-123");

        var task = CreateTask(projectId, "another-member");
        var originalStatus = task.Status;

        var service = CreateService();

        var result = await service.UpdateStatusAsync(
            task.Id,
            new UpdateTaskStatusRequest
            {
                Status = TaskStatus.Completed,
                RowVersion = Convert.ToBase64String(task.RowVersion)
            },
            "member-123");

        Assert.Null(result);
        Assert.Equal(originalStatus, task.Status);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldAllowManager()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "manager-123", ProjectMemberRole.Manager);

        var task = CreateTask(projectId);

        var service = CreateService();

        var result = await service.UpdateStatusAsync(
            task.Id,
            new UpdateTaskStatusRequest
            {
                Status = TaskStatus.Completed,
                RowVersion = Convert.ToBase64String(task.RowVersion)
            },
            "manager-123");

        Assert.NotNull(result);
        Assert.Equal(TaskStatus.Completed, result.Status);
        Assert.Empty(_notifications.AddedNotifications);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteTaskForManager()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "manager-123", ProjectMemberRole.Manager);

        var task = CreateTask(projectId);

        var service = CreateService();

        var result = await service.DeleteAsync(task.Id, "manager-123");

        Assert.True(result);
        Assert.True(task.IsDeleted);
        Assert.NotNull(task.DeletedAtUtc);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRejectNonManager()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "member-123");

        var task = CreateTask(projectId);

        var service = CreateService();

        var result = await service.DeleteAsync(task.Id, "member-123");

        Assert.False(result);
        Assert.False(task.IsDeleted);
        Assert.Null(task.DeletedAtUtc);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    private sealed class FakeTaskRepository : ITaskRepository
    {
        public List<TaskItem> AddedTasks { get; } = [];

        public byte[]? ReceivedOriginalRowVersion { get; private set; }

        public Task<TaskItem?> GetByIdAsync(
            Guid taskId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                AddedTasks.FirstOrDefault(t => t.Id == taskId));
        }

        public Task<IReadOnlyList<TaskItem>> GetByProjectIdAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<TaskItem> result = AddedTasks
                .Where(t => t.ProjectId == projectId && !t.IsDeleted)
                .OrderByDescending(t => t.CreatedAtUtc)
                .ToList();

            return Task.FromResult(result);
        }

        public void SetOriginalRowVersion(TaskItem task, byte[] rowVersion)
        {
            ReceivedOriginalRowVersion = rowVersion;
        }

        public void Add(TaskItem task)
        {
            AddedTasks.Add(task);
        }

        public void Update(TaskItem task)
{
}
    }

    private sealed class FakeProjectMemberRepository
        : IProjectMemberRepository
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

        public void Add(ProjectMember member)
        {
            AddedMembers.Add(member);
        }

        public void Remove(ProjectMember member)
        {
            AddedMembers.Remove(member);
        }
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
            return Task.FromResult(
                AddedNotifications.FirstOrDefault(n =>
                    n.Id == notificationId &&
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
                .ToList();

            return Task.FromResult(result);
        }

        public Task<int> CountByRecipientAsync(
            string recipientUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                AddedNotifications.Count(n => n.RecipientUserId == recipientUserId));
        }

        public Task<int> CountUnreadByRecipientAsync(
            string recipientUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                AddedNotifications.Count(n =>
                    n.RecipientUserId == recipientUserId && !n.IsRead));
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
            }

            return Task.FromResult(notifications.Count);
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCallCount { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            ReceivedCancellationToken = cancellationToken;

            return Task.FromResult(1);
        }
    }
}

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
    private readonly FakeProjectRepository _projects = new();
    private readonly FakeProjectMemberRepository _members = new();
    private readonly FakeUserAccountRepository _users = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeAuditLogRepository _auditLogs = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private TaskService CreateService()
    {
        return new TaskService(
            _tasks,
            _projects,
            _members,
            _users,
            _notifications,
            _auditLogs,
            _unitOfWork,
            new CreateTaskRequestValidator(),
            new UpdateTaskRequestValidator(),
            new UpdateTaskStatusRequestValidator(),
            new AssignTaskRequestValidator(),
            new SmartTask.BLL.Validators.TaskListQueryRequestValidator(),
            new FakeCurrentRequestContext());
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

        if (!_users.AddedAccounts.Any(account => account.Id == userId))
        {
            _users.AddedAccounts.Add(new UserAccount
            {
                Id = userId,
                DisplayName = userId,
                Email = $"{userId}@example.test",
                IsActive = true
            });
        }
    }

    private void AddGlobalRole(string userId, string role)
    {
        var existingRoles = _users.Roles.TryGetValue(userId, out var roles)
            ? roles.ToList()
            : [];

        if (!existingRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
        {
            existingRoles.Add(role);
        }

        _users.Roles[userId] = existingRoles;
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

        if (!_projects.AddedProjects.Any(project => project.Id == task.ProjectId))
        {
            _projects.AddedProjects.Add(new Project
            {
                Id = task.ProjectId,
                Name = "Test project",
                Status = ProjectStatus.Active,
                CreatedByUserId = "manager-123"
            });
        }

        return task;
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateTaskForManager()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "manager-123", ProjectMemberRole.Manager);
        AddGlobalRole("manager-123", "Project Manager");
        AddMember(projectId, "member-123");

        _projects.AddedProjects.Add(new Project
        {
            Id = projectId,
            Name = "Test project",
            Status = ProjectStatus.Active,
            CreatedByUserId = "manager-123"
        });

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
        AddGlobalRole("manager-123", "Project Manager");

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
        AddGlobalRole("manager-123", "Project Manager");

        _projects.AddedProjects.Add(new Project
        {
            Id = projectId,
            Name = "Test project",
            Status = ProjectStatus.Active,
            CreatedByUserId = "manager-123"
        });

        var service = CreateService();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => service.CreateAsync(
                projectId,
                new CreateTaskRequest
                {
                    Title = "New task",
                    AssignedToUserId = "outsider-123"
                },
                "manager-123"));

        Assert.Contains(
            exception.Errors,
            error => error.PropertyName == "AssignedToUserId");
        Assert.Empty(_tasks.AddedTasks);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_ShouldTreatEmptyAssigneeAsUnassigned()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "manager-123", ProjectMemberRole.Manager);
        AddGlobalRole("manager-123", "Project Manager");

        _projects.AddedProjects.Add(new Project
        {
            Id = projectId,
            Name = "Test project",
            Status = ProjectStatus.Active,
            CreatedByUserId = "manager-123"
        });

        var result = await CreateService().CreateAsync(
            projectId,
            new CreateTaskRequest
            {
                Title = "Unassigned task",
                AssignedToUserId = "   "
            },
            "manager-123");

        Assert.NotNull(result);
        Assert.Null(result.AssignedToUserId);
        Assert.Single(_tasks.AddedTasks);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectAssigneeWhenProjectIsNotActive()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "manager-123", ProjectMemberRole.Manager);
        AddMember(projectId, "member-123");
        AddGlobalRole("manager-123", "Project Manager");

        _projects.AddedProjects.Add(new Project
        {
            Id = projectId,
            Name = "Planning project",
            Status = ProjectStatus.Planning,
            CreatedByUserId = "manager-123"
        });

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => CreateService().CreateAsync(
                projectId,
                new CreateTaskRequest
                {
                    Title = "Assigned too early",
                    AssignedToUserId = "member-123"
                },
                "manager-123"));

        Assert.Contains(
            exception.Errors,
            error =>
                error.PropertyName == "AssignedToUserId" &&
                error.ErrorMessage.Contains("active project", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(_tasks.AddedTasks);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnNullForAdminWhenProjectDoesNotExist()
    {
        AddGlobalRole("admin-123", "Admin");

        var result = await CreateService().CreateAsync(
            Guid.NewGuid(),
            new CreateTaskRequest { Title = "Orphan task" },
            "admin-123");

        Assert.Null(result);
        Assert.Empty(_tasks.AddedTasks);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnNullForAdminWhenProjectIsSoftDeleted()
    {
        AddGlobalRole("admin-123", "Admin");
        var projectId = Guid.NewGuid();

        _projects.AddedProjects.Add(new Project
        {
            Id = projectId,
            Name = "Deleted project",
            Status = ProjectStatus.Active,
            CreatedByUserId = "admin-123",
            IsDeleted = true
        });

        var result = await CreateService().CreateAsync(
            projectId,
            new CreateTaskRequest { Title = "Task in deleted project" },
            "admin-123");

        Assert.Null(result);
        Assert.Empty(_tasks.AddedTasks);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task GetByProjectIdAsync_ShouldReturnTasksForProjectMember()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "member-123");
        AddGlobalRole("member-123", "Team Member");

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
    public async Task GetPagedByProjectIdAsync_ShouldReturnPaginationMetadata()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "member-123");
        AddGlobalRole("member-123", "Team Member");

        CreateTask(projectId);
        CreateTask(projectId);
        CreateTask(projectId);

        var service = CreateService();

        var result = await service.GetPagedByProjectIdAsync(
            projectId,
            new TaskListQueryRequest
            {
                PageNumber = 1,
                PageSize = 2
            },
            "member-123");

        Assert.NotNull(result);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.Items.Count);
        Assert.False(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
    }

    [Fact]
    public async Task GetPagedByProjectIdAsync_ShouldRejectInvalidQuery()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "member-123");
        AddGlobalRole("member-123", "Team Member");

        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(
            () => service.GetPagedByProjectIdAsync(
                projectId,
                new TaskListQueryRequest { PageSize = 101 },
                "member-123"));
    }

    [Fact]
    public async Task GetPagedByProjectIdAsync_ShouldRejectNonMember()
    {
        var projectId = Guid.NewGuid();
        var service = CreateService();

        var result = await service.GetPagedByProjectIdAsync(
            projectId,
            new TaskListQueryRequest(),
            "outsider-123");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnTaskForProjectMember()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "member-123");
        AddGlobalRole("member-123", "Team Member");

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
        AddGlobalRole("manager-123", "Project Manager");

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
        AddGlobalRole("manager-123", "Project Manager");
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
    public async Task AssignAsync_ShouldAssignTaskAndNotifyAssignee()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "manager-123", ProjectMemberRole.Manager);
        AddMember(projectId, "member-123");
        _users.Roles["manager-123"] = ["Project Manager"];

        var task = CreateTask(projectId);
        var rowVersion = Convert.ToBase64String(task.RowVersion);

        var result = await CreateService().AssignAsync(
            task.Id,
            new AssignTaskRequest
            {
                AssignedToUserId = "member-123",
                RowVersion = rowVersion
            },
            "manager-123");

        Assert.NotNull(result);
        Assert.Equal("member-123", result.AssignedToUserId);
        Assert.Equal("member-123", task.AssignedToUserId);
        Assert.Equal(rowVersion, Convert.ToBase64String(
            _tasks.ReceivedOriginalRowVersion!));

        var notification = Assert.Single(_notifications.AddedNotifications);
        Assert.Equal("member-123", notification.RecipientUserId);
        Assert.Equal("TaskAssigned", notification.Type);
        Assert.Equal(task.Id, notification.RelatedEntityId);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task AssignAsync_ShouldRejectInactiveAssignee()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "manager-123", ProjectMemberRole.Manager);
        AddMember(projectId, "inactive-member");
        _users.Roles["manager-123"] = ["Project Manager"];

        _users.AddedAccounts
            .Single(account => account.Id == "inactive-member")
            .IsActive = false;

        var task = CreateTask(projectId);

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().AssignAsync(
                task.Id,
                new AssignTaskRequest
                {
                    AssignedToUserId = "inactive-member",
                    RowVersion = Convert.ToBase64String(task.RowVersion)
                },
                "manager-123"));

        Assert.Null(task.AssignedToUserId);
        Assert.Empty(_notifications.AddedNotifications);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task AssignAsync_ShouldRejectCallerWithoutAssignmentPermission()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "member-123", ProjectMemberRole.Manager);
        AddMember(projectId, "target-member");

        // Project membership alone does not grant the global Project Manager role.
        var task = CreateTask(projectId);

        var result = await CreateService().AssignAsync(
            task.Id,
            new AssignTaskRequest
            {
                AssignedToUserId = "target-member",
                RowVersion = Convert.ToBase64String(task.RowVersion)
            },
            "member-123");

        Assert.Null(result);
        Assert.Null(task.AssignedToUserId);
        Assert.Empty(_notifications.AddedNotifications);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task AssignAsync_ShouldRejectInactiveProject()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "manager-123", ProjectMemberRole.Manager);
        AddMember(projectId, "member-123");
        _users.Roles["manager-123"] = ["Project Manager"];

        var task = CreateTask(projectId);
        _projects.AddedProjects.Single(project => project.Id == projectId)
            .Status = ProjectStatus.OnHold;

        var result = await CreateService().AssignAsync(
            task.Id,
            new AssignTaskRequest
            {
                AssignedToUserId = "member-123",
                RowVersion = Convert.ToBase64String(task.RowVersion)
            },
            "manager-123");

        Assert.Null(result);
        Assert.Null(task.AssignedToUserId);
        Assert.Empty(_notifications.AddedNotifications);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldAllowAssignedMember()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "member-123");
        AddGlobalRole("member-123", "Team Member");

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
        AddGlobalRole("manager-123", "Project Manager");

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
        AddGlobalRole("manager-123", "Project Manager");

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

    [Fact]
    public async Task CreateAsync_ShouldRecordTaskCreatedAudit()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "manager-123", ProjectMemberRole.Manager);
        AddGlobalRole("manager-123", "Project Manager");
        _projects.AddedProjects.Add(new Project
        {
            Id = projectId,
            Name = "Test project",
            Status = ProjectStatus.Active,
            CreatedByUserId = "manager-123"
        });

        var result = await CreateService().CreateAsync(
            projectId,
            new CreateTaskRequest { Title = "Audited task" },
            "manager-123");

        Assert.NotNull(result);
        var audit = Assert.Single(_auditLogs.AddedLogs);
        Assert.Equal("TaskCreated", audit.Action);
        Assert.Equal("Task", audit.EntityName);
        Assert.Equal(result.Id.ToString(), audit.EntityId);
        Assert.Equal("manager-123", audit.UserId);
        Assert.Equal(FakeCurrentRequestContext.DefaultIpAddress, audit.IpAddress);
        Assert.Contains(projectId.ToString(), audit.Details);
    }

    [Fact]
    public async Task UpdateAsync_ShouldRecordChangedFieldsAndAssignmentAudit()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "manager-123", ProjectMemberRole.Manager);
        AddGlobalRole("manager-123", "Project Manager");
        AddMember(projectId, "member-123");

        var task = CreateTask(projectId);

        var result = await CreateService().UpdateAsync(
            task.Id,
            new UpdateTaskRequest
            {
                Title = "Test task",
                Description = "Changed description",
                Priority = task.Priority,
                AssignedToUserId = "member-123",
                RowVersion = Convert.ToBase64String(task.RowVersion)
            },
            "manager-123");

        Assert.NotNull(result);
        Assert.Equal(2, _auditLogs.AddedLogs.Count);

        var updated = Assert.Single(_auditLogs.AddedLogs, a => a.Action == "TaskUpdated");
        Assert.Contains("Description", updated.Details);
        Assert.Contains("AssignedToUserId", updated.Details);
        Assert.DoesNotContain("\"Title\"", updated.Details);

        var assigned = Assert.Single(_auditLogs.AddedLogs, a => a.Action == "TaskAssigned");
        Assert.Contains("member-123", assigned.Details);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task AssignAsync_ShouldRecordTaskAssignedAudit()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "manager-123", ProjectMemberRole.Manager);
        AddMember(projectId, "member-123");
        _users.Roles["manager-123"] = ["Project Manager"];

        var task = CreateTask(projectId);

        await CreateService().AssignAsync(
            task.Id,
            new AssignTaskRequest
            {
                AssignedToUserId = "member-123",
                RowVersion = Convert.ToBase64String(task.RowVersion)
            },
            "manager-123");

        var audit = Assert.Single(_auditLogs.AddedLogs);
        Assert.Equal("TaskAssigned", audit.Action);
        Assert.Equal(task.Id.ToString(), audit.EntityId);
        Assert.Contains("\"NewAssigneeId\":\"member-123\"", audit.Details);
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldRecordStatusChangeAudit()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "member-123");
        AddGlobalRole("member-123", "Team Member");

        var task = CreateTask(projectId, "member-123");

        await CreateService().UpdateStatusAsync(
            task.Id,
            new UpdateTaskStatusRequest
            {
                Status = TaskStatus.Completed,
                RowVersion = Convert.ToBase64String(task.RowVersion)
            },
            "member-123");

        var audit = Assert.Single(_auditLogs.AddedLogs);
        Assert.Equal("TaskStatusChanged", audit.Action);
        Assert.Equal("member-123", audit.UserId);
        Assert.Contains("\"PreviousStatus\":\"ToDo\"", audit.Details);
        Assert.Contains("\"NewStatus\":\"Completed\"", audit.Details);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRecordTaskDeletedAudit()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "manager-123", ProjectMemberRole.Manager);
        AddGlobalRole("manager-123", "Project Manager");

        var task = CreateTask(projectId);

        await CreateService().DeleteAsync(task.Id, "manager-123");

        var audit = Assert.Single(_auditLogs.AddedLogs);
        Assert.Equal("TaskDeleted", audit.Action);
        Assert.Equal(task.Id.ToString(), audit.EntityId);
    }

    [Fact]
    public async Task DeleteAsync_WhenRejected_ShouldNotRecordAudit()
    {
        var projectId = Guid.NewGuid();
        AddMember(projectId, "member-123");
        AddGlobalRole("member-123", "Team Member");

        var task = CreateTask(projectId);

        var result = await CreateService().DeleteAsync(task.Id, "member-123");

        Assert.False(result);
        Assert.Empty(_auditLogs.AddedLogs);
    }

    private sealed class FakeProjectRepository : IProjectRepository
    {
        public List<Project> AddedProjects { get; } = [];

        public Task<Project?> GetByIdAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                AddedProjects.FirstOrDefault(project => project.Id == projectId));
        }

        public Task<IReadOnlyList<Project>> GetByMemberUserIdAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Project> result = AddedProjects
                .Where(project => project.Members.Any(member => member.UserId == userId))
                .ToList();

            return Task.FromResult(result);
        }

        public Task<(IReadOnlyList<Project> Items, int TotalCount)>
            GetPagedByMemberUserIdAsync(
                string userId,
                SmartTask.BLL.DTOs.Projects.ProjectListQueryRequest query,
                CancellationToken cancellationToken = default)
        {
            return Task.FromResult<(IReadOnlyList<Project>, int)>(([], 0));
        }

        public Task<IReadOnlyList<Project>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Project>>(AddedProjects);
        }

        public Task<(IReadOnlyList<Project> Items, int TotalCount)> GetPagedAllAsync(
            SmartTask.BLL.DTOs.Projects.ProjectListQueryRequest query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<(IReadOnlyList<Project>, int)>(
                (AddedProjects, AddedProjects.Count));
        }

        public void Add(Project project) => AddedProjects.Add(project);

        public void Update(Project project) { }

        public void SetOriginalRowVersion(Project project, byte[] rowVersion) { }
    }

    private sealed class FakeUserAccountRepository : IUserAccountRepository
    {
        public List<UserAccount> AddedAccounts { get; } = [];

        public Dictionary<string, IReadOnlyList<string>> Roles { get; } = [];

        public Task<(bool Succeeded, string UserId, IReadOnlyList<string> Errors)>
            CreateUserAsync(
                string displayName,
                string email,
                string password,
                CancellationToken cancellationToken = default)
        {
            return Task.FromResult<(bool, string, IReadOnlyList<string>)>(
                (false, string.Empty, []));
        }

        public Task<UserAccount?> FindByEmailAsync(
            string email,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                AddedAccounts.FirstOrDefault(account => account.Email == email));
        }

        public Task<UserAccount?> FindByIdAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                AddedAccounts.FirstOrDefault(account => account.Id == userId));
        }

        public Task<bool> CheckPasswordAsync(
            string userId,
            string password,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<IReadOnlyList<string>> GetRolesAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Roles.TryGetValue(userId, out var roles)
                    ? roles
                    : (IReadOnlyList<string>)[]);
        }

        public Task<(bool Succeeded, IReadOnlyList<string> Errors)> AddToRoleAsync(
            string userId,
            string role,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<(bool, IReadOnlyList<string>)>((false, []));
        }
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
                AddedTasks.FirstOrDefault(
                    t => t.Id == taskId && !t.IsDeleted));
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

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCallCount { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

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
            ReceivedCancellationToken = cancellationToken;

            return Task.FromResult(1);
        }
    }
}

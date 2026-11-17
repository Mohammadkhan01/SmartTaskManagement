using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartTask.BLL.DTOs.Tasks;
using SmartTask.DAL.Identity;
using SmartTask.DAL.Persistence;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.IntegrationTests;

public sealed class TaskHttpIntegrationTests
{
    [SqlServerFact]
    public async Task TaskEndpoints_EnforceProjectMembershipAndTaskPermissions()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "SMARTTASK_TEST_CONNECTION_STRING")!;

        var connectionBuilder =
            new SqlConnectionStringBuilder(connectionString);

        Assert.Equal("SmartTaskTestDb", connectionBuilder.InitialCatalog);

        await using var factory =
            new ProjectTestWebApplicationFactory(connectionString);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        var options = new DbContextOptionsBuilder<SmartTaskDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var managerId = Guid.NewGuid().ToString();
        var assignedUserId = Guid.NewGuid().ToString();
        var memberId = Guid.NewGuid().ToString();
        var outsiderId = Guid.NewGuid().ToString();
        var projectId = Guid.NewGuid();

        try
        {
            // Arrange: create users, project, and project memberships.
            await using (var context = new SmartTaskDbContext(options))
            {
                context.Users.AddRange(
                    CreateUser(managerId),
                    CreateUser(assignedUserId),
                    CreateUser(memberId),
                    CreateUser(outsiderId));

                context.Projects.Add(new Project
                {
                    Id = projectId,
                    Name = "Task Integration Test Project",
                    Status = ProjectStatus.Active,
                    CreatedByUserId = managerId
                });

                context.ProjectMembers.AddRange(
                    new ProjectMember
                    {
                        ProjectId = projectId,
                        UserId = managerId,
                        ProjectRole = ProjectMemberRole.Manager
                    },
                    new ProjectMember
                    {
                        ProjectId = projectId,
                        UserId = assignedUserId,
                        ProjectRole = ProjectMemberRole.Member
                    },
                    new ProjectMember
                    {
                        ProjectId = projectId,
                        UserId = memberId,
                        ProjectRole = ProjectMemberRole.Member
                    });

                await AddGlobalRoleAsync(context, "PROJECT MANAGER", managerId);
                await AddGlobalRoleAsync(
                    context,
                    "TEAM MEMBER",
                    assignedUserId,
                    memberId,
                    outsiderId);

                await context.SaveChangesAsync();
            }

            // 1. Manager creates a task.
            AuthenticateAs(client, managerId);

            var createRequest = new CreateTaskRequest
            {
                Title = "Integration Test Task",
                Description = "Created through the HTTP API.",
                Priority = TaskPriority.High,
                AssignedToUserId = assignedUserId
            };

            var createResponse = await client.PostAsJsonAsync(
                $"/api/v1/projects/{projectId}/tasks",
                createRequest);

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            var createdTask =
                await createResponse.Content.ReadFromJsonAsync<TaskResponse>();

            Assert.NotNull(createdTask);
            Assert.Equal(projectId, createdTask.ProjectId);
            Assert.Equal(managerId, createdTask.CreatedByUserId);
            Assert.Equal(assignedUserId, createdTask.AssignedToUserId);
            Assert.False(string.IsNullOrWhiteSpace(createdTask.RowVersion));

            var taskId = createdTask.Id;

            // 2. A project member can retrieve the project task list.
            AuthenticateAs(client, memberId);

            var listResponse = await client.GetAsync(
                $"/api/v1/projects/{projectId}/tasks");

            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

            var tasks =
                await listResponse.Content.ReadFromJsonAsync<TaskListResponse>();

            Assert.NotNull(tasks);
            Assert.Contains(tasks.Items, task => task.Id == taskId);
            Assert.Equal(1, tasks.TotalCount);
            Assert.Equal(1, tasks.TotalPages);

            // 2a. Seed additional tasks to exercise SQL-backed query behavior.
            var alphaTaskId = Guid.NewGuid();
            var betaTaskId = Guid.NewGuid();
            var gammaTaskId = Guid.NewGuid();

            await using (var context = new SmartTaskDbContext(options))
            {
                context.Tasks.AddRange(
                    new TaskItem
                    {
                        Id = alphaTaskId,
                        ProjectId = projectId,
                        Title = "Alpha planning",
                        Description = "Prepare the project plan.",
                        Status = SmartTask.Domain.Enums.TaskStatus.ToDo,
                        Priority = TaskPriority.Low,
                        AssignedToUserId = memberId,
                        CreatedByUserId = managerId,
                        CreatedAtUtc = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc),
                        DueDateUtc = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc)
                    },
                    new TaskItem
                    {
                        Id = betaTaskId,
                        ProjectId = projectId,
                        Title = "Beta release",
                        Description = "Prepare the release checklist.",
                        Status = SmartTask.Domain.Enums.TaskStatus.InProgress,
                        Priority = TaskPriority.High,
                        AssignedToUserId = memberId,
                        CreatedByUserId = managerId,
                        CreatedAtUtc = new DateTime(2026, 11, 11, 9, 0, 0, DateTimeKind.Utc),
                        DueDateUtc = new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc)
                    },
                    new TaskItem
                    {
                        Id = gammaTaskId,
                        ProjectId = projectId,
                        Title = "Gamma support",
                        Description = "Handle customer support requests.",
                        Status = SmartTask.Domain.Enums.TaskStatus.Completed,
                        Priority = TaskPriority.Medium,
                        AssignedToUserId = assignedUserId,
                        CreatedByUserId = managerId,
                        CreatedAtUtc = new DateTime(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc),
                        DueDateUtc = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc)
                    });

                await context.SaveChangesAsync();
            }

            // 2b. Search and combined filters execute against SQL Server.
            var filteredResponse = await client.GetAsync(
                $"/api/v1/projects/{projectId}/tasks?search=release&status=InProgress&priority=High&assignedToUserId={memberId}");

            Assert.Equal(HttpStatusCode.OK, filteredResponse.StatusCode);

            var filteredTasks =
                await filteredResponse.Content.ReadFromJsonAsync<TaskListResponse>();

            Assert.NotNull(filteredTasks);
            var filteredTask = Assert.Single(filteredTasks.Items);
            Assert.Equal(betaTaskId, filteredTask.Id);
            Assert.Equal(1, filteredTasks.TotalCount);

            // 2c. Sorting and pagination return the expected ordered page.
            var pagedResponse = await client.GetAsync(
                $"/api/v1/projects/{projectId}/tasks?pageNumber=1&pageSize=2&sortBy=title&sortDescending=false");

            Assert.Equal(HttpStatusCode.OK, pagedResponse.StatusCode);

            var pagedTasks =
                await pagedResponse.Content.ReadFromJsonAsync<TaskListResponse>();

            Assert.NotNull(pagedTasks);
            Assert.Equal(4, pagedTasks.TotalCount);
            Assert.Equal(2, pagedTasks.TotalPages);
            Assert.Equal(2, pagedTasks.Items.Count);
            Assert.Equal(alphaTaskId, pagedTasks.Items[0].Id);
            Assert.Equal(betaTaskId, pagedTasks.Items[1].Id);
            Assert.False(pagedTasks.HasPreviousPage);
            Assert.True(pagedTasks.HasNextPage);

            // 2d. Due-date range filtering returns only matching tasks.
            var dueDateResponse = await client.GetAsync(
                $"/api/v1/projects/{projectId}/tasks?dueDateFromUtc=2026-11-17T00:00:00Z&dueDateToUtc=2026-11-18T23:59:59Z");

            Assert.Equal(HttpStatusCode.OK, dueDateResponse.StatusCode);

            var dueDateTasks =
                await dueDateResponse.Content.ReadFromJsonAsync<TaskListResponse>();

            Assert.NotNull(dueDateTasks);
            Assert.Equal(2, dueDateTasks.TotalCount);
            Assert.Contains(dueDateTasks.Items, task => task.Id == betaTaskId);
            Assert.Contains(dueDateTasks.Items, task => task.Id == gammaTaskId);

            // 3. A non-member cannot retrieve the project task list.
            AuthenticateAs(client, outsiderId);

            var outsiderListResponse = await client.GetAsync(
                $"/api/v1/projects/{projectId}/tasks");

            Assert.Equal(
                HttpStatusCode.NotFound,
                outsiderListResponse.StatusCode);

            // 4. A project member can retrieve an individual task.
            AuthenticateAs(client, memberId);

            var getResponse = await client.GetAsync(
                $"/api/v1/tasks/{taskId}");

            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            // 5. A non-member cannot retrieve an individual task.
            AuthenticateAs(client, outsiderId);

            var outsiderGetResponse = await client.GetAsync(
                $"/api/v1/tasks/{taskId}");

            Assert.Equal(
                HttpStatusCode.NotFound,
                outsiderGetResponse.StatusCode);

            // 6. The assigned member can update the task status.
            AuthenticateAs(client, assignedUserId);

            var statusRequest = new UpdateTaskStatusRequest
            {
                Status = SmartTask.Domain.Enums.TaskStatus.InProgress,
                RowVersion = createdTask.RowVersion
            };

            var statusResponse = await client.PutAsJsonAsync(
                $"/api/v1/tasks/{taskId}/status",
                statusRequest);

            Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

            var statusUpdatedTask =
                await statusResponse.Content.ReadFromJsonAsync<TaskResponse>();

            Assert.NotNull(statusUpdatedTask);
            Assert.Equal(
                SmartTask.Domain.Enums.TaskStatus.InProgress,
                statusUpdatedTask.Status);

            // 7. An unassigned member cannot update the task status.
            AuthenticateAs(client, memberId);

            var unauthorizedStatusRequest = new UpdateTaskStatusRequest
            {
                Status = SmartTask.Domain.Enums.TaskStatus.Completed,
                RowVersion = statusUpdatedTask.RowVersion
            };

            var unauthorizedStatusResponse = await client.PutAsJsonAsync(
                $"/api/v1/tasks/{taskId}/status",
                unauthorizedStatusRequest);

            Assert.Equal(
                HttpStatusCode.NotFound,
                unauthorizedStatusResponse.StatusCode);

            // 8. A manager can update task details.
            AuthenticateAs(client, managerId);

            var updateRequest = new UpdateTaskRequest
            {
                Title = "Updated Integration Test Task",
                Description = "Updated by the project manager.",
                Priority = TaskPriority.Critical,
                AssignedToUserId = assignedUserId,
                RowVersion = statusUpdatedTask.RowVersion
            };

            var updateResponse = await client.PutAsJsonAsync(
                $"/api/v1/tasks/{taskId}",
                updateRequest);

            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            var updatedTask =
                await updateResponse.Content.ReadFromJsonAsync<TaskResponse>();

            Assert.NotNull(updatedTask);
            Assert.Equal("Updated Integration Test Task", updatedTask.Title);
            Assert.Equal(TaskPriority.Critical, updatedTask.Priority);

            // 9. A manager can delete a task.
            var deleteResponse = await client.DeleteAsync(
                $"/api/v1/tasks/{taskId}");

            Assert.Equal(
                HttpStatusCode.NoContent,
                deleteResponse.StatusCode);

            // 10. A soft-deleted task is no longer accessible.
            AuthenticateAs(client, memberId);

            var deletedTaskResponse = await client.GetAsync(
                $"/api/v1/tasks/{taskId}");

            Assert.Equal(
                HttpStatusCode.NotFound,
                deletedTaskResponse.StatusCode);

            // 11. Task lifecycle actions are persisted as audit history.
            await using (var auditContext = new SmartTaskDbContext(options))
            {
                var auditActions = await auditContext.AuditLogs
                    .AsNoTracking()
                    .Where(a =>
                        a.EntityName == "Task" &&
                        a.EntityId == taskId.ToString())
                    .Select(a => a.Action)
                    .ToListAsync();

                Assert.Contains("TaskCreated", auditActions);
                Assert.Contains("TaskStatusChanged", auditActions);
                Assert.Contains("TaskUpdated", auditActions);
                Assert.Contains("TaskDeleted", auditActions);
            }
        }
        finally
        {
            await CleanupAsync(options, projectId,
                managerId, assignedUserId, memberId, outsiderId);
        }
    }

    [SqlServerFact]
    public async Task AssignTask_AsProjectManager_PersistsAssignmentAndNotification()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "SMARTTASK_TEST_CONNECTION_STRING")!;

        var connectionBuilder =
            new SqlConnectionStringBuilder(connectionString);

        Assert.Equal("SmartTaskTestDb", connectionBuilder.InitialCatalog);

        await using var factory =
            new ProjectTestWebApplicationFactory(connectionString);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        var options = new DbContextOptionsBuilder<SmartTaskDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var managerId = Guid.NewGuid().ToString();
        var assigneeId = Guid.NewGuid().ToString();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        try
        {
            byte[] originalRowVersion;

            await using (var context = new SmartTaskDbContext(options))
            {
                context.Users.AddRange(
                    CreateUser(managerId),
                    CreateUser(assigneeId));

                var managerRole = await context.Roles
                    .SingleAsync(role =>
                        role.NormalizedName == "PROJECT MANAGER");

                context.UserRoles.Add(new IdentityUserRole<string>
                {
                    UserId = managerId,
                    RoleId = managerRole.Id
                });

                context.Projects.Add(new Project
                {
                    Id = projectId,
                    Name = "Task Assignment Integration Project",
                    CreatedByUserId = managerId,
                    Status = ProjectStatus.Active
                });

                context.ProjectMembers.AddRange(
                    new ProjectMember
                    {
                        ProjectId = projectId,
                        UserId = managerId,
                        ProjectRole = ProjectMemberRole.Manager
                    },
                    new ProjectMember
                    {
                        ProjectId = projectId,
                        UserId = assigneeId,
                        ProjectRole = ProjectMemberRole.Member
                    });

                context.Tasks.Add(new TaskItem
                {
                    Id = taskId,
                    ProjectId = projectId,
                    Title = "Assignment Integration Test",
                    CreatedByUserId = managerId,
                    Priority = TaskPriority.High
                });

                await context.SaveChangesAsync();

                var task = await context.Tasks
                    .AsNoTracking()
                    .SingleAsync(t => t.Id == taskId);

                originalRowVersion = task.RowVersion.ToArray();
            }

            AuthenticateAs(client, managerId);

            var request = new AssignTaskRequest
            {
                AssignedToUserId = assigneeId,
                RowVersion = Convert.ToBase64String(originalRowVersion)
            };

            var response = await client.PutAsJsonAsync(
                $"/api/v1/tasks/{taskId}/assign",
                request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var responseTask =
                await response.Content.ReadFromJsonAsync<TaskResponse>();

            Assert.NotNull(responseTask);
            Assert.Equal(taskId, responseTask.Id);
            Assert.Equal(assigneeId, responseTask.AssignedToUserId);
            Assert.False(string.IsNullOrWhiteSpace(responseTask.RowVersion));
            Assert.NotEqual(
                Convert.ToBase64String(originalRowVersion),
                responseTask.RowVersion);

            await using (var verificationContext =
                new SmartTaskDbContext(options))
            {
                var persistedTask = await verificationContext.Tasks
                    .AsNoTracking()
                    .SingleAsync(t => t.Id == taskId);

                Assert.Equal(assigneeId, persistedTask.AssignedToUserId);
                Assert.NotEqual(
                    originalRowVersion,
                    persistedTask.RowVersion);

                var notification = await verificationContext.Notifications
                    .AsNoTracking()
                    .SingleAsync(n =>
                        n.RecipientUserId == assigneeId &&
                        n.RelatedEntityId == taskId &&
                        n.RelatedEntityType == "Task");

                Assert.Equal("TaskAssigned", notification.Type);
                Assert.Equal("Task assigned to you", notification.Title);
            }
        }
        finally
        {
            await CleanupAsync(options, projectId, managerId, assigneeId);
        }
    }

    [SqlServerFact]
    public async Task PutTask_WithStaleRowVersion_Returns409Conflict()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "SMARTTASK_TEST_CONNECTION_STRING")!;

        var connectionBuilder =
            new SqlConnectionStringBuilder(connectionString);

        Assert.Equal("SmartTaskTestDb", connectionBuilder.InitialCatalog);

        await using var factory =
            new ProjectTestWebApplicationFactory(connectionString);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        var options = new DbContextOptionsBuilder<SmartTaskDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var managerId = Guid.NewGuid().ToString();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        try
        {
            byte[] originalRowVersion;

            // Arrange: create a manager, project, membership, and task.
            await using (var context = new SmartTaskDbContext(options))
            {
                context.Users.Add(CreateUser(managerId));

                context.Projects.Add(new Project
                {
                    Id = projectId,
                    Name = "Task Concurrency Project",
                    CreatedByUserId = managerId
                });

                context.ProjectMembers.Add(new ProjectMember
                {
                    ProjectId = projectId,
                    UserId = managerId,
                    ProjectRole = ProjectMemberRole.Manager
                });

                var task = new TaskItem
                {
                    Id = taskId,
                    ProjectId = projectId,
                    Title = "Original Task",
                    CreatedByUserId = managerId,
                    Priority = TaskPriority.Medium
                };

                context.Tasks.Add(task);
                await AddGlobalRoleAsync(context, "PROJECT MANAGER", managerId);

                await context.SaveChangesAsync();

                originalRowVersion = task.RowVersion.ToArray();
            }

            // Simulate another request updating the task.
            await using (var concurrentContext =
                new SmartTaskDbContext(options))
            {
                var task = await concurrentContext.Tasks
                    .SingleAsync(t => t.Id == taskId);

                task.Title = "Updated by another request";

                await concurrentContext.SaveChangesAsync();
            }

           
            // Act: submit an update using the old RowVersion.
            AuthenticateAs(client, managerId);
            var request = new UpdateTaskRequest
            {
                Title = "Stale Task Update",
                Description = "This update must be rejected.",
                Priority = TaskPriority.High,
                RowVersion = Convert.ToBase64String(originalRowVersion)
            };
            var response = await client.PutAsJsonAsync(
                $"/api/v1/tasks/{taskId}",
                request);

            var responseBody = await response.Content.ReadAsStringAsync();

            // Assert: stale update returns HTTP 409 Problem Details.
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Contains("Concurrency conflict", responseBody);
            Assert.Contains("Reload it and try again", responseBody);
        }
        finally
        {
            await CleanupAsync(options, projectId, managerId);
        }
    }

    [SqlServerFact]
    public async Task PatchTaskStatus_WithStaleRowVersion_Returns409Conflict()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "SMARTTASK_TEST_CONNECTION_STRING")!;

        var connectionBuilder =
            new SqlConnectionStringBuilder(connectionString);

        Assert.Equal("SmartTaskTestDb", connectionBuilder.InitialCatalog);

        await using var factory =
            new ProjectTestWebApplicationFactory(connectionString);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        var options = new DbContextOptionsBuilder<SmartTaskDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var managerId = Guid.NewGuid().ToString();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        try
        {
            byte[] originalRowVersion;

            // Arrange: create manager, project, membership, and task.
            await using (var context = new SmartTaskDbContext(options))
            {
                context.Users.Add(CreateUser(managerId));

                context.Projects.Add(new Project
                {
                    Id = projectId,
                    Name = "Task Status Concurrency Project",
                    CreatedByUserId = managerId
                });

                context.ProjectMembers.Add(new ProjectMember
                {
                    ProjectId = projectId,
                    UserId = managerId,
                    ProjectRole = ProjectMemberRole.Manager
                });

                var task = new TaskItem
                {
                    Id = taskId,
                    ProjectId = projectId,
                    Title = "Original Task",
                    CreatedByUserId = managerId,
                    Priority = TaskPriority.Medium
                };

                context.Tasks.Add(task);
                await AddGlobalRoleAsync(context, "PROJECT MANAGER", managerId);
                await context.SaveChangesAsync();

                originalRowVersion = task.RowVersion.ToArray();
            }

            // Simulate another request updating the task.
            await using (var concurrentContext =
                new SmartTaskDbContext(options))
            {
                var task = await concurrentContext.Tasks
                    .SingleAsync(t => t.Id == taskId);

                task.Title = "Updated by another request";

                await concurrentContext.SaveChangesAsync();
            }

            // Act: submit a status update using the stale RowVersion.
            AuthenticateAs(client, managerId);

            var request = new UpdateTaskStatusRequest
            {
                Status = SmartTask.Domain.Enums.TaskStatus.InProgress,
                RowVersion = Convert.ToBase64String(originalRowVersion)
            };

            var response = await client.PutAsJsonAsync(
                $"/api/v1/tasks/{taskId}/status",
                request);

            var responseBody = await response.Content.ReadAsStringAsync();

            // Assert: stale status update returns HTTP 409.
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Contains("Concurrency conflict", responseBody);
            Assert.Contains("Reload it and try again", responseBody);
            // Verify the concurrent update remains persisted.
await using (var verificationContext = new SmartTaskDbContext(options))
{
    var persistedTask = await verificationContext.Tasks
        .AsNoTracking()
        .SingleAsync(t => t.Id == taskId);

    Assert.Equal("Updated by another request", persistedTask.Title);
}
        }
        finally
        {
            await CleanupAsync(options, projectId, managerId);
        }
    }




    private static ApplicationUser CreateUser(string userId)
    {
        var email = $"task-test-{Guid.NewGuid():N}@example.com";

        return new ApplicationUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };
    }

    private static async Task AddGlobalRoleAsync(
        SmartTaskDbContext context,
        string normalizedRoleName,
        params string[] userIds)
    {
        var role = await context.Roles
            .SingleAsync(r => r.NormalizedName == normalizedRoleName);

        foreach (var userId in userIds)
        {
            context.UserRoles.Add(new IdentityUserRole<string>
            {
                UserId = userId,
                RoleId = role.Id
            });
        }
    }

    private static void AuthenticateAs(HttpClient client, string userId)
    {
        client.DefaultRequestHeaders.Remove("X-Test-UserId");
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
    }

    private static async Task CleanupAsync(
        DbContextOptions<SmartTaskDbContext> options,
        Guid projectId,
        params string[] userIds)
    {
        await using var context = new SmartTaskDbContext(options);

        // Audit history references users with a restrictive FK.
        context.AuditLogs.RemoveRange(await context.AuditLogs
            .Where(a => a.UserId != null && userIds.Contains(a.UserId))
            .ToListAsync());

        // Delete tasks first because the project-task FK restricts deletion.
        var tasks = await context.Tasks
            .IgnoreQueryFilters()
            .Where(t => t.ProjectId == projectId)
            .ToListAsync();

        context.Tasks.RemoveRange(tasks);

        // Then delete project memberships.
        var memberships = await context.ProjectMembers
            .IgnoreQueryFilters()
            .Where(pm => pm.ProjectId == projectId)
            .ToListAsync();

        context.ProjectMembers.RemoveRange(memberships);

        // Then delete the project.
        var project = await context.Projects
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(p => p.Id == projectId);

        if (project is not null)
        {
            context.Projects.Remove(project);
        }

        // Finally delete users after all dependent records are removed.
        var notifications = await context.Notifications
            .Where(n => userIds.Contains(n.RecipientUserId))
            .ToListAsync();

        context.Notifications.RemoveRange(notifications);

        var userRoles = await context.UserRoles
            .Where(userRole => userIds.Contains(userRole.UserId))
            .ToListAsync();

        context.UserRoles.RemoveRange(userRoles);

        var users = await context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync();

        context.Users.RemoveRange(users);

        await context.SaveChangesAsync();
    }
}

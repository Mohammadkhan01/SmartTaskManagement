using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartTask.BLL.DTOs.Dashboard;
using SmartTask.DAL.Identity;
using SmartTask.DAL.Persistence;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;
using TaskStatus = SmartTask.Domain.Enums.TaskStatus;
using SmartTask.DAL.Persistence.Repositories;

namespace SmartTask.IntegrationTests;

public sealed class DashboardHttpIntegrationTests
{
    [SqlServerFact]
    public async Task DashboardEndpoints_ReturnCorrectMetricsAndEnforceAccess()
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

        var todayUtc = DateTime.UtcNow.Date;
        var overdueDate = todayUtc.AddDays(-1).AddHours(12);
        var dueTodayDate = todayUtc.AddHours(23);
        var futureDate = todayUtc.AddDays(1).AddHours(12);

        try
        {
            await using (var context = new SmartTaskDbContext(options))
            {
                context.Users.AddRange(
                    CreateUser(managerId, "Dashboard Manager"),
                    CreateUser(assignedUserId, "Assigned User"),
                    CreateUser(memberId, "Project Member"),
                    CreateUser(outsiderId, "Outsider"));

                context.Projects.Add(new Project
                {
                    Id = projectId,
                    Name = "Dashboard Integration Project",
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

                var tasks = new[]
                {
                    CreateTask(
                        projectId,
                        managerId,
                        assignedUserId,
                        "Assigned overdue todo",
                        TaskStatus.ToDo,
                        overdueDate,
                        todayUtc.AddHours(1),
                        TaskPriority.Low),

                    CreateTask(
                        projectId,
                        managerId,
                        assignedUserId,
                        "Assigned due today",
                        TaskStatus.InProgress,
                        dueTodayDate,
                        todayUtc.AddHours(2),
                        TaskPriority.Medium),

                    CreateTask(
                        projectId,
                        managerId,
                        assignedUserId,
                        "Completed overdue task",
                        TaskStatus.Completed,
                        overdueDate,
                        todayUtc.AddHours(3),
                        TaskPriority.High),

                    CreateTask(
                        projectId,
                        managerId,
                        assignedUserId,
                        "Cancelled overdue task",
                        TaskStatus.Cancelled,
                        overdueDate,
                        todayUtc.AddHours(4),
                        TaskPriority.Critical),

                    CreateTask(
                        projectId,
                        managerId,
                        assignedUserId,
                        "Completed future task",
                        TaskStatus.Completed,
                        futureDate,
                        todayUtc.AddHours(5),
                        TaskPriority.Medium),

                    CreateTask(
                        projectId,
                        managerId,
                        assignedUserId,
                        "Assigned upcoming todo task",
                        TaskStatus.ToDo,
                        futureDate,
                        todayUtc.AddHours(6),
                        TaskPriority.Critical)
                };

                var deletedTask = CreateTask(
                    projectId,
                    managerId,
                    assignedUserId,
                    "Soft deleted task",
                    TaskStatus.ToDo,
                    overdueDate,
                    todayUtc.AddHours(7));

                deletedTask.IsDeleted = true;
                deletedTask.DeletedAtUtc = DateTime.UtcNow;

                context.Tasks.AddRange(tasks);
                context.Tasks.Add(deletedTask);

                var taskCreationTimes = tasks
                    .Select(task => (Id: task.Id, CreatedAtUtc: task.CreatedAtUtc))
                    .Append((
                        Id: deletedTask.Id,
                        CreatedAtUtc: deletedTask.CreatedAtUtc))
                    .ToArray();

                await context.SaveChangesAsync();

                // SaveChanges resets CreatedAtUtc for newly added entities.
                // Restore deterministic timestamps for recent-task ordering.
                foreach (var taskCreationTime in taskCreationTimes)
                {
                    await context.Tasks
                        .IgnoreQueryFilters()
                        .Where(task => task.Id == taskCreationTime.Id)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(
                                task => task.CreatedAtUtc,
                                taskCreationTime.CreatedAtUtc));
                }
            }

            // 1. My dashboard includes only tasks assigned to this user.
            AuthenticateAs(client, assignedUserId);

            var myResponse = await client.GetAsync("/api/v1/dashboard/summary");

            Assert.Equal(HttpStatusCode.OK, myResponse.StatusCode);

            var myDashboard =
                await myResponse.Content
                    .ReadFromJsonAsync<DashboardSummaryResponse>();

            Assert.NotNull(myDashboard);
            Assert.Null(myDashboard.ProjectId);
            Assert.Equal(6, myDashboard.TotalTasks);
            Assert.Equal(2, myDashboard.ToDoTasks);
            Assert.Equal(1, myDashboard.InProgressTasks);
            Assert.Equal(2, myDashboard.CompletedTasks);
            Assert.Equal(1, myDashboard.CancelledTasks);
            Assert.Equal(1, myDashboard.OverdueTasks);
            Assert.Equal(1, myDashboard.DueTodayTasks);
            Assert.Equal(33.33m, myDashboard.CompletionPercentage);
            Assert.Equal(6, myDashboard.RecentTasks.Count);
            Assert.DoesNotContain(
                myDashboard.RecentTasks,
                task => task.Title == "Soft deleted task");

            Assert.NotNull(myDashboard.ProjectTotals);
            Assert.Equal(1, myDashboard.ProjectTotals.TotalProjects);
            Assert.Equal(1, myDashboard.ProjectTotals.PlanningProjects);
            Assert.Equal(0, myDashboard.ProjectTotals.ActiveProjects);

            // 1a. Personal task statistics include status, priority and upcoming aggregates.
            var statisticsResponse = await client.GetAsync(
                "/api/v1/dashboard/task-statistics");

            Assert.Equal(HttpStatusCode.OK, statisticsResponse.StatusCode);

            var statistics =
                await statisticsResponse.Content
                    .ReadFromJsonAsync<DashboardTaskStatisticsResponse>();

            Assert.NotNull(statistics);
            Assert.Equal(6, statistics.TotalTasks);
            Assert.Equal(2, statistics.ToDoTasks);
            Assert.Equal(1, statistics.InProgressTasks);
            Assert.Equal(2, statistics.CompletedTasks);
            Assert.Equal(1, statistics.CancelledTasks);
            Assert.Equal(3, statistics.PendingTasks);
            Assert.Equal(33.33m, statistics.CompletionPercentage);

            Assert.Equal(1, statistics.LowPriorityTasks);
            Assert.Equal(2, statistics.MediumPriorityTasks);
            Assert.Equal(1, statistics.HighPriorityTasks);
            Assert.Equal(2, statistics.CriticalPriorityTasks);

            Assert.Equal(2, statistics.UpcomingTasks.Count);
            Assert.Equal(
                "Assigned due today",
                statistics.UpcomingTasks[0].Title);
            Assert.Equal(
                "Assigned upcoming todo task",
                statistics.UpcomingTasks[1].Title);

            Assert.DoesNotContain(
                statistics.UpcomingTasks,
                task => task.Title.Contains("Completed"));
            Assert.DoesNotContain(
                statistics.UpcomingTasks,
                task => task.Title.Contains("Cancelled"));
            Assert.DoesNotContain(
                statistics.UpcomingTasks,
                task => task.Title == "Soft deleted task");

            // 2. Any project member can view project-wide metrics.
            AuthenticateAs(client, memberId);

            var projectResponse = await client.GetAsync(
                $"/api/v1/dashboard/projects/{projectId}");

            Assert.Equal(HttpStatusCode.OK, projectResponse.StatusCode);

            var projectDashboard =
                await projectResponse.Content
                    .ReadFromJsonAsync<DashboardSummaryResponse>();

            Assert.NotNull(projectDashboard);
            Assert.Equal(projectId, projectDashboard.ProjectId);
            Assert.Equal("Dashboard Integration Project",
                projectDashboard.ProjectName);
            Assert.Equal(6, projectDashboard.TotalTasks);
            Assert.Equal(2, projectDashboard.ToDoTasks);
            Assert.Equal(1, projectDashboard.InProgressTasks);
            Assert.Equal(2, projectDashboard.CompletedTasks);
            Assert.Equal(1, projectDashboard.CancelledTasks);
            Assert.Equal(1, projectDashboard.OverdueTasks);
            Assert.Equal(1, projectDashboard.DueTodayTasks);
            Assert.Equal(33.33m, projectDashboard.CompletionPercentage);
            Assert.Equal(6, projectDashboard.RecentTasks.Count);
            Assert.Equal(
                "Assigned upcoming todo task",
                projectDashboard.RecentTasks[0].Title);
            Assert.Null(projectDashboard.ProjectTotals);

            // 2a. Without membership, project totals are empty.
            AuthenticateAs(client, outsiderId);

            var outsiderSummary = await client.GetFromJsonAsync<DashboardSummaryResponse>(
                "/api/v1/dashboard/summary");

            Assert.NotNull(outsiderSummary?.ProjectTotals);
            Assert.Equal(0, outsiderSummary.ProjectTotals.TotalProjects);

            AuthenticateAs(client, memberId);

            // 3. A regular member cannot view workload reporting.
            var memberWorkloadResponse = await client.GetAsync(
                $"/api/v1/dashboard/projects/{projectId}/workload");

            Assert.Equal(
                HttpStatusCode.NotFound,
                memberWorkloadResponse.StatusCode);

            // 4. A non-member cannot view the project dashboard or workload.
            AuthenticateAs(client, outsiderId);

            var outsiderDashboardResponse = await client.GetAsync(
                $"/api/v1/dashboard/projects/{projectId}");

            Assert.Equal(
                HttpStatusCode.NotFound,
                outsiderDashboardResponse.StatusCode);

            var outsiderWorkloadResponse = await client.GetAsync(
                $"/api/v1/dashboard/projects/{projectId}/workload");

            Assert.Equal(
                HttpStatusCode.NotFound,
                outsiderWorkloadResponse.StatusCode);

            // 5. A manager can view workload reporting.
            AuthenticateAs(client, managerId);

            var workloadResponse = await client.GetAsync(
                $"/api/v1/dashboard/projects/{projectId}/workload");

            Assert.Equal(HttpStatusCode.OK, workloadResponse.StatusCode);

            var workload =
                await workloadResponse.Content
                    .ReadFromJsonAsync<List<MemberWorkloadResponse>>();

            Assert.NotNull(workload);
            Assert.Equal(3, workload.Count);

            var assignedWorkload = Assert.Single(
                workload,
                item => item.UserId == assignedUserId);

            Assert.Equal("Assigned User", assignedWorkload.DisplayName);
            Assert.Equal(6, assignedWorkload.TotalTasks);
            Assert.Equal(2, assignedWorkload.ToDoTasks);
            Assert.Equal(1, assignedWorkload.InProgressTasks);
            Assert.Equal(2, assignedWorkload.CompletedTasks);
            Assert.Equal(1, assignedWorkload.OverdueTasks);

            var managerWorkload = Assert.Single(
                workload,
                item => item.UserId == managerId);

            Assert.Equal(0, managerWorkload.TotalTasks);
        }
        finally
        {
            await CleanupAsync(
                options,
                projectId,
                managerId,
                assignedUserId,
                memberId,
                outsiderId);
        }
    }

    private static ApplicationUser CreateUser(
        string userId,
        string displayName)
    {
        var email = $"dashboard-{Guid.NewGuid():N}@example.com";

        return new ApplicationUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            DisplayName = displayName,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };
    }

    private static TaskItem CreateTask(
        Guid projectId,
        string createdByUserId,
        string? assignedToUserId,
        string title,
        TaskStatus status,
        DateTime? dueDateUtc,
        DateTime createdAtUtc,
        TaskPriority priority = TaskPriority.Medium)
    {
        return new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            CreatedByUserId = createdByUserId,
            AssignedToUserId = assignedToUserId,
            Title = title,
            Status = status,
            Priority = priority,
            DueDateUtc = dueDateUtc,
            CreatedAtUtc = createdAtUtc
        };
    }

    private static void AuthenticateAs(
        HttpClient client,
        string userId)
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

        var tasks = await context.Tasks
            .IgnoreQueryFilters()
            .Where(task => task.ProjectId == projectId)
            .ToListAsync();

        context.Tasks.RemoveRange(tasks);

        var memberships = await context.ProjectMembers
            .IgnoreQueryFilters()
            .Where(member => member.ProjectId == projectId)
            .ToListAsync();

        context.ProjectMembers.RemoveRange(memberships);

        var project = await context.Projects
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == projectId);

        if (project is not null)
        {
            context.Projects.Remove(project);
        }

        var notifications = await context.Notifications
            .Where(notification =>
                userIds.Contains(notification.RecipientUserId))
            .ToListAsync();

        context.Notifications.RemoveRange(notifications);

        var users = await context.Users
            .Where(user => userIds.Contains(user.Id))
            .ToListAsync();

        context.Users.RemoveRange(users);

        await context.SaveChangesAsync();
    }
}

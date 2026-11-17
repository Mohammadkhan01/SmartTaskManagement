using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartTask.BLL.DTOs.TaskComments;
using SmartTask.DAL.Identity;
using SmartTask.DAL.Persistence;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.IntegrationTests;

public sealed class TaskCommentActivityHttpTests
{

    [SqlServerFact]
    public async Task PutComment_WithStaleRowVersion_Returns409Conflict()
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

        var memberId = Guid.NewGuid().ToString();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        try
        {
            // Arrange: create a project member and task.
            await using (var context = new SmartTaskDbContext(options))
            {
                context.Users.Add(CreateUser(memberId));

                context.Projects.Add(new Project
                {
                    Id = projectId,
                    Name = "Comment Concurrency Test Project",
                    CreatedByUserId = memberId
                });

                context.ProjectMembers.Add(new ProjectMember
                {
                    ProjectId = projectId,
                    UserId = memberId,
                    ProjectRole = ProjectMemberRole.Manager
                });

                context.Tasks.Add(new TaskItem
                {
                    Id = taskId,
                    ProjectId = projectId,
                    Title = "Comment Concurrency Test Task",
                    CreatedByUserId = memberId,
                    Priority = TaskPriority.Medium
                });

                await context.SaveChangesAsync();
            }

            // Create a comment through the API.
            AuthenticateAs(client, memberId);

            var createResponse = await client.PostAsJsonAsync(
                $"/api/tasks/{taskId}/comments",
                new CreateTaskCommentRequest
                {
                    Content = "Original comment."
                });

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            var createdComment =
                await createResponse.Content
                    .ReadFromJsonAsync<TaskCommentResponse>();

            Assert.NotNull(createdComment);
            Assert.False(
                string.IsNullOrWhiteSpace(createdComment.RowVersion));

            // Simulate another request updating the comment.
            await using (var context = new SmartTaskDbContext(options))
            {
                var comment = await context.TaskComments
                    .SingleAsync(c => c.Id == createdComment.Id);

                comment.Content = "Updated by concurrent request.";

                await context.SaveChangesAsync();
            }

            // Submit an update using the original, now-stale RowVersion.
            var staleUpdateResponse = await client.PutAsJsonAsync(
                $"/api/task-comments/{createdComment.Id}",
                new UpdateTaskCommentRequest
                {
                    Content = "This stale update must not be saved.",
                    RowVersion = createdComment.RowVersion
                });

            Assert.Equal(
                HttpStatusCode.Conflict,
                staleUpdateResponse.StatusCode);

            // Verify the concurrent update was not overwritten.
            await using (var context = new SmartTaskDbContext(options))
            {
                var persistedComment = await context.TaskComments
                    .SingleAsync(c => c.Id == createdComment.Id);

                Assert.Equal(
                    "Updated by concurrent request.",
                    persistedComment.Content);
            }
        }
        finally
        {
            await CleanupAsync(
                options,
                projectId,
                taskId,
                memberId);
        }
    }


    [SqlServerFact]
    public async Task CommentAndActivityEndpoints_EnforcePermissionsAndRecordHistory()
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
        var memberId = Guid.NewGuid().ToString();
        var outsiderId = Guid.NewGuid().ToString();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        try
        {
            // Arrange: create users, project, membership, and task.
            await using (var context = new SmartTaskDbContext(options))
            {
                context.Users.AddRange(
                    CreateUser(managerId),
                    CreateUser(memberId),
                    CreateUser(outsiderId));

                context.Projects.Add(new Project
                {
                    Id = projectId,
                    Name = "Comment Integration Test Project",
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
                        UserId = memberId,
                        ProjectRole = ProjectMemberRole.Member
                    });

                context.Tasks.Add(new TaskItem
                {
                    Id = taskId,
                    ProjectId = projectId,
                    Title = "Comment Integration Test Task",
                    CreatedByUserId = managerId,
                    Priority = TaskPriority.Medium
                });

                await context.SaveChangesAsync();
            }

            // 1. A project member creates a comment.
            AuthenticateAs(client, memberId);

            var createResponse = await client.PostAsJsonAsync(
                $"/api/tasks/{taskId}/comments",
                new CreateTaskCommentRequest
                {
                    Content = "Initial integration test comment."
                });

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            var createdComment =
                await createResponse.Content
                    .ReadFromJsonAsync<TaskCommentResponse>();

            Assert.NotNull(createdComment);
            Assert.Equal(taskId, createdComment.TaskId);
            Assert.Equal(memberId, createdComment.UserId);
            Assert.Equal(
                "Initial integration test comment.",
                createdComment.Content);
            Assert.False(
                string.IsNullOrWhiteSpace(createdComment.RowVersion));

            // 2. The author updates their comment.
            var updateResponse = await client.PutAsJsonAsync(
                $"/api/task-comments/{createdComment.Id}",
                new UpdateTaskCommentRequest
                {
                    Content = "Updated integration test comment.",
                    RowVersion = createdComment.RowVersion
                });

            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            var updatedComment =
                await updateResponse.Content
                    .ReadFromJsonAsync<TaskCommentResponse>();

            Assert.NotNull(updatedComment);
            Assert.Equal(
                "Updated integration test comment.",
                updatedComment.Content);

            // 3. Another project member cannot update someone else's comment.
            AuthenticateAs(client, managerId);

            var unauthorizedUpdateResponse = await client.PutAsJsonAsync(
                $"/api/task-comments/{createdComment.Id}",
                new UpdateTaskCommentRequest
                {
                    Content = "Unauthorized update attempt.",
                    RowVersion = updatedComment.RowVersion
                });

            Assert.Equal(
                HttpStatusCode.NotFound,
                unauthorizedUpdateResponse.StatusCode);

            // 4. A project member can list task comments.
            AuthenticateAs(client, managerId);

            var listResponse = await client.GetAsync(
                $"/api/tasks/{taskId}/comments");

            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

            var comments =
                await listResponse.Content
                    .ReadFromJsonAsync<List<TaskCommentResponse>>();

            Assert.NotNull(comments);
            Assert.Contains(comments, comment =>
                comment.Id == createdComment.Id);

            // 5. A project member can read task activity history.
            AuthenticateAs(client, memberId);

            var activityResponse = await client.GetAsync(
                $"/api/tasks/{taskId}/activity");

            Assert.Equal(HttpStatusCode.OK, activityResponse.StatusCode);

            var activities =
                await activityResponse.Content
                    .ReadFromJsonAsync<List<TaskActivityResponse>>();

            Assert.NotNull(activities);
            Assert.Contains(activities, activity =>
                activity.Action == "CommentCreated");
            Assert.Contains(activities, activity =>
                activity.Action == "CommentUpdated");

            // 6. An outsider cannot access comments or activity history.
            AuthenticateAs(client, outsiderId);

            var outsiderCommentsResponse = await client.GetAsync(
                $"/api/tasks/{taskId}/comments");

            Assert.Equal(
                HttpStatusCode.NotFound,
                outsiderCommentsResponse.StatusCode);

            var outsiderActivityResponse = await client.GetAsync(
                $"/api/tasks/{taskId}/activity");

            Assert.Equal(
                HttpStatusCode.NotFound,
                outsiderActivityResponse.StatusCode);

            // 7. A manager can delete another member's comment.
            AuthenticateAs(client, managerId);

            var deleteResponse = await client.DeleteAsync(
                $"/api/task-comments/{createdComment.Id}");

            Assert.Equal(
                HttpStatusCode.NoContent,
                deleteResponse.StatusCode);

            // 8. A deleted comment is no longer returned.
            var afterDeleteResponse = await client.GetAsync(
                $"/api/tasks/{taskId}/comments");

            Assert.Equal(
                HttpStatusCode.OK,
                afterDeleteResponse.StatusCode);

            var remainingComments =
                await afterDeleteResponse.Content
                    .ReadFromJsonAsync<List<TaskCommentResponse>>();

            Assert.NotNull(remainingComments);
            Assert.DoesNotContain(remainingComments, comment =>
                comment.Id == createdComment.Id);

            // 9. The delete action is also recorded in activity history.
            AuthenticateAs(client, memberId);

            var finalActivityResponse = await client.GetAsync(
                $"/api/tasks/{taskId}/activity");

            Assert.Equal(
                HttpStatusCode.OK,
                finalActivityResponse.StatusCode);

            var finalActivities =
                await finalActivityResponse.Content
                    .ReadFromJsonAsync<List<TaskActivityResponse>>();

            Assert.NotNull(finalActivities);
            Assert.Contains(finalActivities, activity =>
                activity.Action == "CommentDeleted");
        }
        finally
        {
            await CleanupAsync(
                options,
                projectId,
                taskId,
                managerId,
                memberId,
                outsiderId);
        }
    }

    private static ApplicationUser CreateUser(string userId)
    {
        var email = $"comment-test-{Guid.NewGuid():N}@example.com";

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

    private static void AuthenticateAs(HttpClient client, string userId)
    {
        client.DefaultRequestHeaders.Remove("X-Test-UserId");
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
    }

    private static async Task CleanupAsync(
        DbContextOptions<SmartTaskDbContext> options,
        Guid projectId,
        Guid taskId,
        params string[] userIds)
    {
        await using var context = new SmartTaskDbContext(options);

        // Remove activity records before deleting their referenced users.
        var activities = await context.AuditLogs
            .Where(a =>
                a.EntityType == "Task" &&
                a.EntityId == taskId.ToString())
            .ToListAsync();

        context.AuditLogs.RemoveRange(activities);

        // Remove comments before deleting the task.
        var comments = await context.TaskComments
            .IgnoreQueryFilters()
            .Where(c => c.TaskId == taskId)
            .ToListAsync();

        context.TaskComments.RemoveRange(comments);

        // Remove tasks before deleting the project.
        var tasks = await context.Tasks
            .IgnoreQueryFilters()
            .Where(t => t.ProjectId == projectId)
            .ToListAsync();

        context.Tasks.RemoveRange(tasks);

        var memberships = await context.ProjectMembers
            .IgnoreQueryFilters()
            .Where(pm => pm.ProjectId == projectId)
            .ToListAsync();

        context.ProjectMembers.RemoveRange(memberships);

        var project = await context.Projects
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(p => p.Id == projectId);

        if (project is not null)
        {
            context.Projects.Remove(project);
        }

        var users = await context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync();

        context.Users.RemoveRange(users);

        await context.SaveChangesAsync();
    }
}

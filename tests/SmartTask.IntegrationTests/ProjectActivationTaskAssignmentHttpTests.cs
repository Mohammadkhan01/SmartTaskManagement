using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartTask.BLL.DTOs.Projects;
using SmartTask.BLL.DTOs.Tasks;
using SmartTask.DAL.Identity;
using SmartTask.DAL.Persistence;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.IntegrationTests;

public sealed class ProjectActivationTaskAssignmentHttpTests
{
    [SqlServerFact]
    public async Task NewProject_CanBeActivatedAndThenAcceptTaskAssignment()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "SMARTTASK_TEST_CONNECTION_STRING")!;

        Assert.Equal(
            "SmartTaskTestDb",
            new SqlConnectionStringBuilder(connectionString).InitialCatalog);

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
        Guid? projectId = null;

        try
        {
            await using (var context = new SmartTaskDbContext(options))
            {
                context.Users.AddRange(CreateUser(managerId), CreateUser(memberId));

                var managerRole = await context.Roles
                    .SingleAsync(role => role.NormalizedName == "PROJECT MANAGER");

                context.UserRoles.Add(new IdentityUserRole<string>
                {
                    UserId = managerId,
                    RoleId = managerRole.Id
                });

                await context.SaveChangesAsync();
            }

            client.DefaultRequestHeaders.Add("X-Test-UserId", managerId);

            // 1. A new project starts in Planning.
            var createProjectResponse = await client.PostAsJsonAsync(
                "/api/v1/projects",
                new CreateProjectRequest { Name = "Activation test project" });

            Assert.Equal(HttpStatusCode.Created, createProjectResponse.StatusCode);

            var project = await createProjectResponse.Content
                .ReadFromJsonAsync<ProjectResponse>();

            Assert.NotNull(project);
            Assert.Equal(ProjectStatus.Planning, project.Status);
            projectId = project.Id;

            var addMemberResponse = await client.PostAsJsonAsync(
                $"/api/v1/projects/{project.Id}/members",
                new AddProjectMemberRequest { UserId = memberId });

            Assert.True(addMemberResponse.IsSuccessStatusCode);

            // 2. Assignment is rejected while the project is still Planning.
            var createTaskResponse = await client.PostAsJsonAsync(
                $"/api/v1/projects/{project.Id}/tasks",
                new CreateTaskRequest { Title = "Activation task" });

            Assert.Equal(HttpStatusCode.Created, createTaskResponse.StatusCode);

            var task = await createTaskResponse.Content
                .ReadFromJsonAsync<TaskResponse>();

            Assert.NotNull(task);

            var planningAssignResponse = await client.PutAsJsonAsync(
                $"/api/v1/tasks/{task.Id}/assign",
                new AssignTaskRequest
                {
                    AssignedToUserId = memberId,
                    RowVersion = task.RowVersion
                });

            Assert.Equal(HttpStatusCode.NotFound, planningAssignResponse.StatusCode);

            // 3. The manager activates the project through the update endpoint.
            var activateResponse = await client.PutAsJsonAsync(
                $"/api/v1/projects/{project.Id}",
                new UpdateProjectRequest
                {
                    Name = project.Name,
                    Description = project.Description,
                    Status = ProjectStatus.Active,
                    RowVersion = project.RowVersion
                });

            Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

            var activated = await activateResponse.Content
                .ReadFromJsonAsync<ProjectResponse>();

            Assert.NotNull(activated);
            Assert.Equal(ProjectStatus.Active, activated.Status);

            // 4. The task can now be assigned to a project member.
            var assignResponse = await client.PutAsJsonAsync(
                $"/api/v1/tasks/{task.Id}/assign",
                new AssignTaskRequest
                {
                    AssignedToUserId = memberId,
                    RowVersion = task.RowVersion
                });

            Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);

            var assigned = await assignResponse.Content
                .ReadFromJsonAsync<TaskResponse>();

            Assert.NotNull(assigned);
            Assert.Equal(memberId, assigned.AssignedToUserId);
        }
        finally
        {
            await CleanupAsync(options, projectId, managerId, memberId);
        }
    }

    private static ApplicationUser CreateUser(string userId)
    {
        var email = $"activation-test-{Guid.NewGuid():N}@example.com";

        return new ApplicationUser
        {
            Id = userId,
            DisplayName = email,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };
    }

    private static async Task CleanupAsync(
        DbContextOptions<SmartTaskDbContext> options,
        Guid? projectId,
        params string[] userIds)
    {
        await using var context = new SmartTaskDbContext(options);

        // Audit history references users with a restrictive FK.
        context.AuditLogs.RemoveRange(await context.AuditLogs
            .Where(a => a.UserId != null && userIds.Contains(a.UserId))
            .ToListAsync());

        if (projectId.HasValue)
        {
            context.Tasks.RemoveRange(await context.Tasks
                .IgnoreQueryFilters()
                .Where(t => t.ProjectId == projectId.Value)
                .ToListAsync());

            context.ProjectMembers.RemoveRange(await context.ProjectMembers
                .IgnoreQueryFilters()
                .Where(pm => pm.ProjectId == projectId.Value)
                .ToListAsync());

            var project = await context.Projects
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(p => p.Id == projectId.Value);

            if (project is not null)
            {
                context.Projects.Remove(project);
            }
        }

        context.Notifications.RemoveRange(await context.Notifications
            .Where(n => userIds.Contains(n.RecipientUserId))
            .ToListAsync());

        context.UserRoles.RemoveRange(await context.UserRoles
            .Where(userRole => userIds.Contains(userRole.UserId))
            .ToListAsync());

        context.Users.RemoveRange(await context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync());

        await context.SaveChangesAsync();
    }
}

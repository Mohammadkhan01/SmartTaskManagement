using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartTask.BLL.DTOs.Dashboard;
using SmartTask.Common.Constants;
using SmartTask.DAL.Identity;
using SmartTask.DAL.Persistence;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;
using TaskStatus = SmartTask.Domain.Enums.TaskStatus;

namespace SmartTask.IntegrationTests;

public sealed class AdminDashboardHttpIntegrationTests
{
    private const string TestIssuer = "SmartTaskIntegrationTests";
    private const string TestSigningKey =
        "IntegrationTestSigningKeyMustBeAtLeast32Chars";

    [SqlServerFact]
    public async Task AdminDashboard_ReturnsSystemTotalsForAdminsOnly()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "SMARTTASK_TEST_CONNECTION_STRING")!;

        Assert.Equal(
            "SmartTaskTestDb",
            new SqlConnectionStringBuilder(connectionString).InitialCatalog);

        var options = new DbContextOptionsBuilder<SmartTaskDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var factory =
            new InactiveUserJwtWebApplicationFactory(connectionString);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        var adminId = Guid.NewGuid().ToString();
        var memberId = Guid.NewGuid().ToString();
        var inactiveId = Guid.NewGuid().ToString();
        var projectId = Guid.NewGuid();

        try
        {
            await using (var context = new SmartTaskDbContext(options))
            {
                context.Users.AddRange(
                    CreateUser(adminId, isActive: true),
                    CreateUser(memberId, isActive: true),
                    CreateUser(inactiveId, isActive: false));

                context.Projects.Add(new Project
                {
                    Id = projectId,
                    Name = "Admin Dashboard Project",
                    Status = ProjectStatus.Active,
                    CreatedByUserId = adminId
                });

                context.Tasks.AddRange(
                    CreateTask(projectId, adminId, TaskStatus.ToDo, DateTime.UtcNow.AddDays(-2)),
                    CreateTask(projectId, adminId, TaskStatus.Completed, null));

                await context.SaveChangesAsync();

                await AddGlobalRoleAsync(context, RoleNames.Admin, adminId);
                await AddGlobalRoleAsync(context, RoleNames.TeamMember, memberId);
            }

            // 1. Anonymous callers are rejected.
            var anonymousResponse = await client.GetAsync("/api/v1/admin/dashboard");

            Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

            // 2. Non-admin users are forbidden.
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    CreateToken(memberId, RoleNames.TeamMember));

            var memberResponse = await client.GetAsync("/api/v1/admin/dashboard");

            Assert.Equal(HttpStatusCode.Forbidden, memberResponse.StatusCode);

            // 3. Admins receive system-wide totals.
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    CreateToken(adminId, RoleNames.Admin));

            var adminResponse = await client.GetAsync("/api/v1/admin/dashboard");

            Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);

            var dashboard =
                await adminResponse.Content.ReadFromJsonAsync<AdminDashboardResponse>();

            Assert.NotNull(dashboard);

            await using (var context = new SmartTaskDbContext(options))
            {
                var nowUtc = DateTime.UtcNow;

                Assert.Equal(
                    await context.Projects.CountAsync(),
                    dashboard.Projects.TotalProjects);
                Assert.Equal(
                    await context.Projects.CountAsync(p => p.Status == ProjectStatus.Active),
                    dashboard.Projects.ActiveProjects);

                Assert.Equal(await context.Tasks.CountAsync(), dashboard.TotalTasks);
                Assert.Equal(
                    await context.Tasks.CountAsync(t => t.Status == TaskStatus.ToDo),
                    dashboard.ToDoTasks);
                Assert.Equal(
                    await context.Tasks.CountAsync(t => t.Status == TaskStatus.Completed),
                    dashboard.CompletedTasks);
                Assert.Equal(
                    await context.Tasks.CountAsync(t =>
                        t.DueDateUtc < nowUtc &&
                        t.Status != TaskStatus.Completed &&
                        t.Status != TaskStatus.Cancelled),
                    dashboard.OverdueTasks);

                var totalUsers = await context.Users.CountAsync();
                var activeUsers = await context.Users.CountAsync(u => u.IsActive);

                Assert.Equal(totalUsers, dashboard.TotalUsers);
                Assert.Equal(activeUsers, dashboard.ActiveUsers);
                Assert.Equal(totalUsers - activeUsers, dashboard.InactiveUsers);
                Assert.Equal(
                    await CountUsersInRoleAsync(context, RoleNames.Admin),
                    dashboard.AdminUsers);
                Assert.Equal(
                    await CountUsersInRoleAsync(context, RoleNames.TeamMember),
                    dashboard.TeamMemberUsers);
            }

            Assert.True(dashboard.Projects.ActiveProjects >= 1);
            Assert.True(dashboard.OverdueTasks >= 1);
            Assert.True(dashboard.InactiveUsers >= 1);
            Assert.True(dashboard.AdminUsers >= 1);
        }
        finally
        {
            await CleanupAsync(options, projectId, adminId, memberId, inactiveId);
        }
    }

    [SqlServerFact]
    public async Task AdminUserEndpoints_AreAdminOnlyAndAuditStatusChanges()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "SMARTTASK_TEST_CONNECTION_STRING")!;

        Assert.Equal(
            "SmartTaskTestDb",
            new SqlConnectionStringBuilder(connectionString).InitialCatalog);

        var options = new DbContextOptionsBuilder<SmartTaskDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var factory =
            new InactiveUserJwtWebApplicationFactory(connectionString);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        var adminId = Guid.NewGuid().ToString();
        var memberId = Guid.NewGuid().ToString();

        try
        {
            await using (var context = new SmartTaskDbContext(options))
            {
                context.Users.AddRange(
                    CreateUser(adminId, isActive: true),
                    CreateUser(memberId, isActive: true));

                await context.SaveChangesAsync();

                await AddGlobalRoleAsync(context, RoleNames.Admin, adminId);
                await AddGlobalRoleAsync(context, RoleNames.TeamMember, memberId);
            }

            var statusRequest = new { IsActive = false };

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                (await client.GetAsync("/api/v1/admin/users")).StatusCode);

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    CreateToken(memberId, RoleNames.TeamMember));

            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await client.GetAsync("/api/v1/admin/users")).StatusCode);
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await client.PatchAsJsonAsync(
                    $"/api/v1/admin/users/{adminId}/status",
                    statusRequest)).StatusCode);
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await client.PutAsJsonAsync(
                    $"/api/v1/admin/users/{memberId}/role",
                    new { Role = RoleNames.Admin })).StatusCode);

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    CreateToken(adminId, RoleNames.Admin));

            Assert.Equal(
                HttpStatusCode.OK,
                (await client.GetAsync("/api/v1/admin/users?pageSize=5")).StatusCode);

            var deactivateResponse = await client.PatchAsJsonAsync(
                $"/api/v1/admin/users/{memberId}/status",
                statusRequest);

            Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

            await using (var context = new SmartTaskDbContext(options))
            {
                Assert.False(await context.Users
                    .Where(u => u.Id == memberId)
                    .Select(u => u.IsActive)
                    .SingleAsync());

                var audit = await context.AuditLogs.SingleAsync(a =>
                    a.EntityName == "User" &&
                    a.EntityId == memberId &&
                    a.Action == "UserStatusChanged");

                Assert.Equal(adminId, audit.UserId);
                Assert.True(audit.Id > 0);
            }

            // Admins cannot deactivate themselves.
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await client.PatchAsJsonAsync(
                    $"/api/v1/admin/users/{adminId}/status",
                    statusRequest)).StatusCode);
        }
        finally
        {
            await using (var context = new SmartTaskDbContext(options))
            {
                await context.AuditLogs
                    .Where(a => a.UserId == adminId || a.EntityId == memberId)
                    .ExecuteDeleteAsync();
            }

            await CleanupAsync(options, Guid.NewGuid(), adminId, memberId);
        }
    }

    private static ApplicationUser CreateUser(string userId, bool isActive)
    {
        var email = $"admin-dashboard-{Guid.NewGuid():N}@example.com";

        return new ApplicationUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            DisplayName = "Admin Dashboard User",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            IsActive = isActive
        };
    }

    private static TaskItem CreateTask(
        Guid projectId,
        string createdByUserId,
        TaskStatus status,
        DateTime? dueDateUtc)
    {
        return new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            CreatedByUserId = createdByUserId,
            Title = $"Admin dashboard task {status}",
            Status = status,
            Priority = TaskPriority.Medium,
            DueDateUtc = dueDateUtc
        };
    }

    private static async Task AddGlobalRoleAsync(
        SmartTaskDbContext context,
        string roleName,
        string userId)
    {
        var roleId = await context.Roles
            .Where(role => role.NormalizedName == roleName.ToUpperInvariant())
            .Select(role => role.Id)
            .SingleAsync();

        context.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<string>
        {
            UserId = userId,
            RoleId = roleId
        });

        await context.SaveChangesAsync();
    }

    private static Task<int> CountUsersInRoleAsync(
        SmartTaskDbContext context,
        string roleName)
    {
        return (
            from userRole in context.UserRoles
            join role in context.Roles on userRole.RoleId equals role.Id
            where role.Name == roleName
            select userRole)
            .CountAsync();
    }

    private static string CreateToken(string userId, string role)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestIssuer,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role)
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task CleanupAsync(
        DbContextOptions<SmartTaskDbContext> options,
        Guid projectId,
        params string[] userIds)
    {
        await using var context = new SmartTaskDbContext(options);

        await context.Tasks
            .IgnoreQueryFilters()
            .Where(task => task.ProjectId == projectId)
            .ExecuteDeleteAsync();

        await context.Projects
            .IgnoreQueryFilters()
            .Where(project => project.Id == projectId)
            .ExecuteDeleteAsync();

        await context.UserRoles
            .Where(userRole => userIds.Contains(userRole.UserId))
            .ExecuteDeleteAsync();

        await context.Users
            .Where(user => userIds.Contains(user.Id))
            .ExecuteDeleteAsync();
    }
}

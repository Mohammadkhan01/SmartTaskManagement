
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartTask.BLL.DTOs.Projects;
using SmartTask.DAL.Identity;
using SmartTask.DAL.Persistence;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.IntegrationTests;

public sealed class ProjectListHttpIntegrationTests
{
    [SqlServerFact]
    public async Task GetMyProjects_AppliesFiltersSortingPaginationAndMembershipScope()
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
        var outsiderId = Guid.NewGuid().ToString();

        var alphaId = Guid.NewGuid();
        var betaId = Guid.NewGuid();
        var planningId = Guid.NewGuid();
        var outsiderProjectId = Guid.NewGuid();

        var projectIds = new[]
        {
            alphaId,
            betaId,
            planningId,
            outsiderProjectId
        };

        try
        {
            // Arrange: create two users and four projects.
            await using (var context = new SmartTaskDbContext(options))
            {
                context.Users.AddRange(
                    CreateUser(memberId),
                    CreateUser(outsiderId));

                context.Projects.AddRange(
                    new Project
                    {
                        Id = alphaId,
                        Name = "Alpha Release",
                        Description = "Prepare the release plan.",
                        Status = ProjectStatus.Active,
                        OwnerId = memberId
                    },
                    new Project
                    {
                        Id = betaId,
                        Name = "Beta Release",
                        Description = "Complete release testing.",
                        Status = ProjectStatus.Active,
                        OwnerId = memberId
                    },
                    new Project
                    {
                        Id = planningId,
                        Name = "Gamma Planning",
                        Description = "Prepare the next project phase.",
                        Status = ProjectStatus.Planning,
                        OwnerId = memberId
                    },
                    new Project
                    {
                        Id = outsiderProjectId,
                        Name = "Hidden Release",
                        Description = "This belongs to another user.",
                        Status = ProjectStatus.Active,
                        OwnerId = outsiderId
                    });

                context.ProjectMembers.AddRange(
                    new ProjectMember
                    {
                        ProjectId = alphaId,
                        UserId = memberId,
                        Role = ProjectMemberRole.Manager
                    },
                    new ProjectMember
                    {
                        ProjectId = betaId,
                        UserId = memberId,
                        Role = ProjectMemberRole.Member
                    },
                    new ProjectMember
                    {
                        ProjectId = planningId,
                        UserId = memberId,
                        Role = ProjectMemberRole.Member
                    },
                    new ProjectMember
                    {
                        ProjectId = outsiderProjectId,
                        UserId = outsiderId,
                        Role = ProjectMemberRole.Manager
                    });

                await context.SaveChangesAsync();
            }

            // Act: request the first page with combined filters.
            AuthenticateAs(client, memberId);

            var firstPageResponse = await client.GetAsync(
                "/api/projects?search=release&status=Active" +
                "&pageNumber=1&pageSize=1" +
                "&sortBy=name&sortDescending=false");

            // Assert: SQL Server applies filters and sorting.
            Assert.Equal(HttpStatusCode.OK, firstPageResponse.StatusCode);

            var firstPage =
                await firstPageResponse.Content
                    .ReadFromJsonAsync<ProjectListResponse>();

            Assert.NotNull(firstPage);

            var firstProject = Assert.Single(firstPage.Items);
            Assert.Equal(alphaId, firstProject.Id);
            Assert.Equal("Alpha Release", firstProject.Name);

            Assert.Equal(1, firstPage.PageNumber);
            Assert.Equal(1, firstPage.PageSize);
            Assert.Equal(2, firstPage.TotalCount);
            Assert.Equal(2, firstPage.TotalPages);
            Assert.False(firstPage.HasPreviousPage);
            Assert.True(firstPage.HasNextPage);

            // Act: request the second page.
            var secondPageResponse = await client.GetAsync(
                "/api/projects?search=release&status=Active" +
                "&pageNumber=2&pageSize=1" +
                "&sortBy=name&sortDescending=false");

            Assert.Equal(HttpStatusCode.OK, secondPageResponse.StatusCode);

            var secondPage =
                await secondPageResponse.Content
                    .ReadFromJsonAsync<ProjectListResponse>();

            Assert.NotNull(secondPage);

            var secondProject = Assert.Single(secondPage.Items);
            Assert.Equal(betaId, secondProject.Id);
            Assert.Equal("Beta Release", secondProject.Name);

            Assert.Equal(2, secondPage.PageNumber);
            Assert.Equal(2, secondPage.TotalCount);
            Assert.Equal(2, secondPage.TotalPages);
            Assert.True(secondPage.HasPreviousPage);
            Assert.False(secondPage.HasNextPage);

            // The outsider's matching project must not appear in either page.
            Assert.DoesNotContain(
                firstPage.Items,
                project => project.Id == outsiderProjectId);

            Assert.DoesNotContain(
                secondPage.Items,
                project => project.Id == outsiderProjectId);
        }
        finally
        {
            await CleanupAsync(options, projectIds, memberId, outsiderId);
        }
    }

    [SqlServerFact]
public async Task GetMyProjects_InvalidPageSize_ReturnsBadRequest()
{
    var connectionString =
        Environment.GetEnvironmentVariable(
            "SMARTTASK_TEST_CONNECTION_STRING")!;

    await using var factory =
        new ProjectTestWebApplicationFactory(connectionString);

    using var client = factory.CreateClient();

    AuthenticateAs(client, Guid.NewGuid().ToString());

    var response = await client.GetAsync(
        "/api/projects?pageNumber=1&pageSize=101");

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

    Assert.Equal(
        "application/problem+json",
        response.Content.Headers.ContentType?.MediaType);

    var problem =
        await response.Content.ReadFromJsonAsync<
            Microsoft.AspNetCore.Mvc.ValidationProblemDetails>();

    Assert.NotNull(problem);
    Assert.Contains("PageSize", problem.Errors.Keys);
}

    [SqlServerFact]
public async Task GetMyProjects_Unauthenticated_ReturnsUnauthorized()
{
    var connectionString =
        Environment.GetEnvironmentVariable(
            "SMARTTASK_TEST_CONNECTION_STRING")!;

    await using var factory =
        new ProjectTestWebApplicationFactory(connectionString);

    using var client = factory.CreateClient();

    // No X-Test-UserId header is provided.
    var response = await client.GetAsync("/api/projects");

    Assert.Equal(
        HttpStatusCode.Unauthorized,
        response.StatusCode);
}


    private static ApplicationUser CreateUser(string userId)
    {
        var email = $"project-list-test-{Guid.NewGuid():N}@example.com";

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
        Guid[] projectIds,
        params string[] userIds)
    {
        await using var context = new SmartTaskDbContext(options);

        var memberships = await context.ProjectMembers
            .IgnoreQueryFilters()
            .Where(pm => projectIds.Contains(pm.ProjectId))
            .ToListAsync();

        context.ProjectMembers.RemoveRange(memberships);

        var projects = await context.Projects
            .IgnoreQueryFilters()
            .Where(p => projectIds.Contains(p.Id))
            .ToListAsync();

        context.Projects.RemoveRange(projects);

        var users = await context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync();

        context.Users.RemoveRange(users);

        await context.SaveChangesAsync();
    }
}

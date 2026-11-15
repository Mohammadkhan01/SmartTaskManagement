using Microsoft.EntityFrameworkCore;
using SmartTask.BLL.DTOs.Projects;
using SmartTask.DAL.Identity;
using SmartTask.DAL.Persistence;
using SmartTask.DAL.Persistence.Repositories;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.UnitTests;

public class ProjectRepositoryPagedTests
{
    private static SmartTaskDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SmartTaskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new SmartTaskDbContext(options);
    }

    private static ApplicationUser CreateUser(string id)
    {
        return new ApplicationUser
        {
            Id = id,
            UserName = id,
            NormalizedUserName = id.ToUpperInvariant()
        };
    }

    [Fact]
    public async Task GetPagedByMemberUserIdAsync_ShouldFilterSortAndPaginate()
    {
        await using var context = CreateDbContext();

        var owner = CreateUser("paged-owner");
        var member = CreateUser("paged-member");
        var outsider = CreateUser("paged-outsider");

        context.Users.AddRange(owner, member, outsider);

        var alpha = new Project
        {
            Name = "Alpha Project",
            Description = "First project",
            OwnerId = owner.Id,
            Status = ProjectStatus.Active
        };

        var beta = new Project
        {
            Name = "Beta Project",
            Description = "Second project",
            OwnerId = owner.Id,
            Status = ProjectStatus.Active
        };

        var gamma = new Project
        {
            Name = "Gamma Project",
            Description = "Third project",
            OwnerId = owner.Id,
            Status = ProjectStatus.Completed
        };

        var hidden = new Project
        {
            Name = "Hidden Project",
            OwnerId = owner.Id,
            Status = ProjectStatus.Active,
            IsDeleted = true,
            DeletedAtUtc = DateTime.UtcNow
        };

        context.Projects.AddRange(alpha, beta, gamma, hidden);

        context.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = alpha.Id, UserId = member.Id },
            new ProjectMember { ProjectId = beta.Id, UserId = member.Id },
            new ProjectMember { ProjectId = gamma.Id, UserId = member.Id },
            new ProjectMember { ProjectId = hidden.Id, UserId = member.Id });

        await context.SaveChangesAsync();

        var repository = new ProjectRepository(context);

        var (items, totalCount) =
            await repository.GetPagedByMemberUserIdAsync(
                member.Id,
                new ProjectListQueryRequest
                {
                    PageNumber = 2,
                    PageSize = 1,
                    Search = "Project",
                    Status = ProjectStatus.Active,
                    SortBy = "name",
                    SortDescending = false
                });

        Assert.Equal(2, totalCount);
        var project = Assert.Single(items);
        Assert.Equal("Beta Project", project.Name);
    }

    [Fact]
    public async Task GetPagedByMemberUserIdAsync_ShouldReturnOnlyMemberProjects()
    {
        await using var context = CreateDbContext();

        var owner = CreateUser("scope-owner");
        var member = CreateUser("scope-member");
        var anotherMember = CreateUser("scope-other-member");

        context.Users.AddRange(owner, member, anotherMember);

        var memberProject = new Project
        {
            Name = "Member Project",
            OwnerId = owner.Id
        };

        var otherProject = new Project
        {
            Name = "Other Project",
            OwnerId = owner.Id
        };

        context.Projects.AddRange(memberProject, otherProject);

        context.ProjectMembers.AddRange(
            new ProjectMember
            {
                ProjectId = memberProject.Id,
                UserId = member.Id
            },
            new ProjectMember
            {
                ProjectId = otherProject.Id,
                UserId = anotherMember.Id
            });

        await context.SaveChangesAsync();

        var repository = new ProjectRepository(context);

        var (items, totalCount) =
            await repository.GetPagedByMemberUserIdAsync(
                member.Id,
                new ProjectListQueryRequest());

        Assert.Equal(1, totalCount);
        var project = Assert.Single(items);
        Assert.Equal(memberProject.Id, project.Id);
    }
}

using Microsoft.EntityFrameworkCore;
using SmartTask.DAL.Identity;
using SmartTask.DAL.Persistence;
using SmartTask.DAL.Persistence.Repositories;
using SmartTask.Domain.Entities;

namespace SmartTask.UnitTests;

public class RepositoryTests
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
    public async Task ProjectRepository_ShouldAddAndRetrieveProject()
    {
        await using var context = CreateDbContext();

        var owner = CreateUser("owner-1");
        context.Users.Add(owner);

        var project = new Project
        {
            Name = "Smart Task Project",
            Description = "Repository test",
            CreatedByUserId = owner.Id
        };

        var repository = new ProjectRepository(context);

        repository.Add(project);
        await context.SaveChangesAsync();

        var result = await repository.GetByIdAsync(project.Id);

        Assert.NotNull(result);
        Assert.Equal("Smart Task Project", result.Name);
        Assert.Equal(owner.Id, result.CreatedByUserId);
    }

    [Fact]
    public async Task ProjectRepository_ShouldNotReturnSoftDeletedProject()
    {
        await using var context = CreateDbContext();

        var owner = CreateUser("owner-2");
        context.Users.Add(owner);

        var project = new Project
        {
            Name = "Deleted Project",
            CreatedByUserId = owner.Id,
            IsDeleted = true,
            DeletedAtUtc = DateTime.UtcNow
        };

        var repository = new ProjectRepository(context);

        repository.Add(project);
        await context.SaveChangesAsync();

        var result = await repository.GetByIdAsync(project.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task ProjectRepository_ShouldReturnOnlyProjectsForSpecificMember()
    {
        await using var context = CreateDbContext();

        var owner = CreateUser("owner-10");
        var user = CreateUser("member-10");
        var anotherUser = CreateUser("member-20");

        context.Users.AddRange(owner, user, anotherUser);

        var project1 = new Project
        {
            Name = "User Project",
            CreatedByUserId = owner.Id
        };

        var project2 = new Project
        {
            Name = "Another User Project",
            CreatedByUserId = owner.Id
        };

        context.Projects.AddRange(project1, project2);

        context.ProjectMembers.AddRange(
            new ProjectMember
            {
                ProjectId = project1.Id,
                UserId = user.Id
            },
            new ProjectMember
            {
                ProjectId = project2.Id,
                UserId = anotherUser.Id
            });

        await context.SaveChangesAsync();

        var repository = new ProjectRepository(context);

        var result = await repository.GetByMemberUserIdAsync(user.Id);

        var project = Assert.Single(result);

        Assert.Equal(project1.Id, project.Id);
        Assert.Equal("User Project", project.Name);
    }

    [Fact]
    public async Task ProjectMemberRepository_ShouldFindMemberByProjectAndUser()
    {
        await using var context = CreateDbContext();

        var owner = CreateUser("owner-3");
        var memberUser = CreateUser("member-1");

        context.Users.AddRange(owner, memberUser);

        var project = new Project
        {
            Name = "Membership Test",
            CreatedByUserId = owner.Id
        };

        context.Projects.Add(project);

        var member = new ProjectMember
        {
            ProjectId = project.Id,
            UserId = memberUser.Id,
            Project = project
        };

        var repository = new ProjectMemberRepository(context);

        repository.Add(member);
        await context.SaveChangesAsync();

        var result = await repository.GetByProjectAndUserAsync(
            project.Id,
            memberUser.Id);

        Assert.NotNull(result);
        Assert.Equal(memberUser.Id, result.UserId);
        Assert.Equal(project.Id, result.ProjectId);
    }

    [Fact]
    public async Task TaskRepository_ShouldAddAndRetrieveTask()
    {
        await using var context = CreateDbContext();

        var owner = CreateUser("owner-4");
        context.Users.Add(owner);

        var project = new Project
        {
            Name = "Task Test Project",
            CreatedByUserId = owner.Id
        };

        context.Projects.Add(project);

        var task = new TaskItem
        {
            ProjectId = project.Id,
            Project = project,
            Title = "Implement repository tests",
            CreatedByUserId = owner.Id
        };

        var repository = new TaskRepository(context);

        repository.Add(task);
        await context.SaveChangesAsync();

        var result = await repository.GetByIdAsync(task.Id);

        Assert.NotNull(result);
        Assert.Equal("Implement repository tests", result.Title);
        Assert.Equal(project.Id, result.ProjectId);
    }

    [Fact]
    public async Task TaskRepository_ShouldExcludeSoftDeletedTasksAndDeletedProjects()
    {
        await using var context = CreateDbContext();

        var owner = CreateUser("owner-task-soft-delete");
        context.Users.Add(owner);

        var activeProject = new Project
        {
            Name = "Active Project",
            CreatedByUserId = owner.Id
        };

        var deletedProject = new Project
        {
            Name = "Deleted Project",
            CreatedByUserId = owner.Id,
            IsDeleted = true,
            DeletedAtUtc = DateTime.UtcNow
        };

        context.Projects.AddRange(activeProject, deletedProject);

        var softDeletedTask = new TaskItem
        {
            ProjectId = activeProject.Id,
            Project = activeProject,
            Title = "Soft deleted task",
            CreatedByUserId = owner.Id,
            IsDeleted = true,
            DeletedAtUtc = DateTime.UtcNow
        };

        var taskInDeletedProject = new TaskItem
        {
            ProjectId = deletedProject.Id,
            Project = deletedProject,
            Title = "Task in deleted project",
            CreatedByUserId = owner.Id
        };

        context.Tasks.AddRange(softDeletedTask, taskInDeletedProject);
        await context.SaveChangesAsync();

        var repository = new TaskRepository(context);

        Assert.Null(await repository.GetByIdAsync(softDeletedTask.Id));
        Assert.Null(await repository.GetByIdAsync(taskInDeletedProject.Id));

        Assert.Empty(await repository.GetByProjectIdAsync(activeProject.Id));
        Assert.Empty(await repository.GetByProjectIdAsync(deletedProject.Id));
    }

}

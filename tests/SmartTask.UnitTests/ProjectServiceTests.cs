using FluentValidation;
using SmartTask.BLL.DTOs.Projects;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.BLL.Services.Projects;
using SmartTask.BLL.Validators;
using SmartTask.Domain.Entities;
using SmartTask.Domain.Enums;

namespace SmartTask.UnitTests;

public class ProjectServiceTests
{
    private readonly FakeProjectRepository _projects = new();
    private readonly FakeProjectMemberRepository _members = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private ProjectService CreateService()
    {
        return new ProjectService(
            _projects,
            _members,
            _unitOfWork,
            new CreateProjectRequestValidator(),
            new UpdateProjectRequestValidator(),
            new ProjectListQueryRequestValidator());
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateProjectAndAssignOwnerAsManager()
    {
        var service = CreateService();

        var request = new CreateProjectRequest
        {
            Name = "  Smart Task  ",
            Description = "  Project description  "
        };

        var result = await service.CreateAsync(request, "user-123");

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Smart Task", result.Name);
        Assert.Equal("Project description", result.Description);
        Assert.Equal("user-123", result.OwnerId);
        Assert.Equal(ProjectStatus.Planning, result.Status);

        Assert.Single(_projects.AddedProjects);
        Assert.Single(_members.AddedMembers);

        var project = _projects.AddedProjects[0];
        var member = _members.AddedMembers[0];

        Assert.Equal(project.Id, member.ProjectId);
        Assert.Equal("user-123", member.UserId);
        Assert.Equal(ProjectMemberRole.Manager, member.Role);

        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectInvalidRequest()
    {
        var service = CreateService();

        var request = new CreateProjectRequest
        {
            Name = "   "
        };

        await Assert.ThrowsAsync<ValidationException>(
            () => service.CreateAsync(request, "user-123"));

        Assert.Empty(_projects.AddedProjects);
        Assert.Empty(_members.AddedMembers);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectEmptyOwnerId()
    {
        var service = CreateService();

        var request = new CreateProjectRequest
        {
            Name = "Valid project"
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAsync(request, " "));

        Assert.Empty(_projects.AddedProjects);
        Assert.Empty(_members.AddedMembers);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_ShouldPassCancellationTokenToUnitOfWork()
    {
        var service = CreateService();
        using var cancellationSource = new CancellationTokenSource();

        await service.CreateAsync(
            new CreateProjectRequest { Name = "Valid project" },
            "user-123",
            cancellationSource.Token);

        Assert.Equal(
            cancellationSource.Token,
            _unitOfWork.ReceivedCancellationToken);
    }
    [Fact]
    public async Task GetMyProjectsAsync_ShouldReturnOnlyProjectsForUser()
    {
        var project = new Project
        {
            Name = "My Project",
            Description = "Test project",
            OwnerId = "user-123",
            Status = ProjectStatus.Active
        };

        var member = new ProjectMember
        {
            ProjectId = project.Id,
            UserId = "user-123",
            Role = ProjectMemberRole.Manager,
            Project = project
        };

        project.Members.Add(member);
        _projects.AddedProjects.Add(project);

        var service = CreateService();

        var result = await service.GetMyProjectsAsync("user-123");

        var response = Assert.Single(result);

        Assert.Equal(project.Id, response.Id);
        Assert.Equal("My Project", response.Name);
        Assert.Equal("Test project", response.Description);
        Assert.Equal("user-123", response.OwnerId);
        Assert.Equal(ProjectStatus.Active, response.Status);
    }

    [Fact]
    public async Task GetMyProjectsAsync_ShouldRejectEmptyUserId()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetMyProjectsAsync(" "));

    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnProjectForMember()
    {
        var project = new Project
        {
            Name = "Member Project",
            OwnerId = "owner-123"
        };

        _projects.AddedProjects.Add(project);

        _members.AddedMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = "member-123",
            Role = ProjectMemberRole.Member
        });

        var service = CreateService();

        var result = await service.GetByIdAsync(
            project.Id,
            "member-123");

        Assert.NotNull(result);
        Assert.Equal(project.Id, result.Id);
        Assert.Equal("Member Project", result.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNullForNonMember()
    {
        var project = new Project
        {
            Name = "Private Project",
            OwnerId = "owner-123"
        };

        _projects.AddedProjects.Add(project);

        var service = CreateService();

        var result = await service.GetByIdAsync(
            project.Id,
            "stranger-123");

        Assert.Null(result);
    }
    private sealed class FakeProjectRepository : IProjectRepository
    {
        public List<Project> AddedProjects { get; } = [];

        public Task<Project?> GetByIdAsync(
    Guid projectId,
    CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                AddedProjects.FirstOrDefault(p => p.Id == projectId));
        }
        public byte[]? OriginalRowVersion { get; private set; }

        public void SetOriginalRowVersion(Project project, byte[] rowVersion)
        {
            OriginalRowVersion = rowVersion.ToArray();
        }
        public Task<IReadOnlyList<Project>> GetByMemberUserIdAsync(
    string userId,
    CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Project> result = AddedProjects
                .Where(p => p.Members.Any(m => m.UserId == userId))
                .OrderByDescending(p => p.CreatedAtUtc)
                .ToList();

            return Task.FromResult(result);
        }
        public Task<(IReadOnlyList<Project> Items, int TotalCount)>
            GetPagedByMemberUserIdAsync(
                string userId,
                ProjectListQueryRequest query,
                CancellationToken cancellationToken = default)
        {
            IEnumerable<Project> projects = AddedProjects
                .Where(p => p.Members.Any(m => m.UserId == userId));

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();

                projects = projects.Where(p =>
                    p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (p.Description != null &&
                     p.Description.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            if (query.Status.HasValue)
            {
                projects = projects.Where(p => p.Status == query.Status.Value);
            }

            var totalCount = projects.Count();
            var descending = query.SortDescending;

            IOrderedEnumerable<Project> orderedProjects =
                query.SortBy.Trim().ToLowerInvariant() switch
                {
                    "createdat" => descending
                        ? projects.OrderByDescending(p => p.CreatedAtUtc)
                        : projects.OrderBy(p => p.CreatedAtUtc),
                    "updatedat" => descending
                        ? projects.OrderByDescending(p => p.UpdatedAtUtc)
                        : projects.OrderBy(p => p.UpdatedAtUtc),
                    "name" => descending
                        ? projects.OrderByDescending(p => p.Name)
                        : projects.OrderBy(p => p.Name),
                    "status" => descending
                        ? projects.OrderByDescending(p => p.Status)
                        : projects.OrderBy(p => p.Status),
                    _ => projects.OrderByDescending(p => p.CreatedAtUtc)
                };

            var skip = (int)((long)(query.PageNumber - 1) * query.PageSize);
            IReadOnlyList<Project> items = orderedProjects
                .ThenBy(p => p.Id)
                .Skip(skip)
                .Take(query.PageSize)
                .ToList();

            return Task.FromResult((items, totalCount));
        }

        public void Add(Project project)
        {
            AddedProjects.Add(project);
        }

        public void Update(Project project)
        {
        }
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateProjectForOwner()
    {
        var project = new Project
        {
            Name = "Old Name",
            Description = "Old Description",
            OwnerId = "owner-123"
        };

        _projects.AddedProjects.Add(project);

        var service = CreateService();

        var result = await service.UpdateAsync(
            project.Id,
           new UpdateProjectRequest
            {
                Name = "New Name",
                Description = "New Description",
                RowVersion = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8])
            },
            "owner-123");

        Assert.NotNull(result);
        Assert.Equal("New Name", result.Name);
        Assert.Equal("New Description", result.Description);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        Assert.Equal(
            new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },
            _projects.OriginalRowVersion);
    }

    [Fact]
    public async Task UpdateAsync_ShouldRejectNonOwner()
    {
        var project = new Project
        {
            Name = "Original Name",
            OwnerId = "owner-123"
        };

        _projects.AddedProjects.Add(project);

        var service = CreateService();

        var result = await service.UpdateAsync(
            project.Id,
            new UpdateProjectRequest { Name = "Changed Name" },
            "another-user");

        Assert.Null(result);
        Assert.Equal("Original Name", project.Name);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteProjectForOwner()
    {
        var project = new Project
        {
            Name = "Project to Delete",
            OwnerId = "owner-123"
        };

        _projects.AddedProjects.Add(project);

        var service = CreateService();

        var result = await service.DeleteAsync(
            project.Id,
            "owner-123");

        Assert.True(result);
        Assert.True(project.IsDeleted);
        Assert.NotNull(project.DeletedAtUtc);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRejectNonOwner()
    {
        var project = new Project
        {
            Name = "Protected Project",
            OwnerId = "owner-123"
        };

        _projects.AddedProjects.Add(project);

        var service = CreateService();

        var result = await service.DeleteAsync(
            project.Id,
            "another-user");

        Assert.False(result);
        Assert.False(project.IsDeleted);
        Assert.Null(project.DeletedAtUtc);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
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

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCallCount { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            ReceivedCancellationToken = cancellationToken;

            return Task.FromResult(2);
        }
    }
}

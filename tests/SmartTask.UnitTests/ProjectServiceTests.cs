using FluentValidation;
using SmartTask.BLL.DTOs.Projects;
using SmartTask.BLL.Exceptions;
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
    private readonly FakeUserAccountRepository _users = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private ProjectService CreateService()
    {
        return new ProjectService(
            _projects,
            _members,
            _users,
            _unitOfWork,
            new CreateProjectRequestValidator(),
            new UpdateProjectRequestValidator(),
            new ProjectListQueryRequestValidator());
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateProjectAndAssignOwnerAsManager()
    {
        _users.Roles["user-123"] = ["Project Manager"];
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
        Assert.Equal("user-123", result.CreatedByUserId);
        Assert.Equal(ProjectStatus.Planning, result.Status);

        Assert.Single(_projects.AddedProjects);
        Assert.Single(_members.AddedMembers);

        var project = _projects.AddedProjects[0];
        var member = _members.AddedMembers[0];

        Assert.Equal(project.Id, member.ProjectId);
        Assert.Equal("user-123", member.UserId);
        Assert.Equal(ProjectMemberRole.Manager, member.ProjectRole);

        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectInvalidRequest()
    {
        _users.Roles["user-123"] = ["Project Manager"];
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
    public async Task CreateAsync_ShouldRejectEmptyCreatedByUserId()
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
        _users.Roles["user-123"] = ["Project Manager"];
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
            CreatedByUserId = "user-123",
            Status = ProjectStatus.Active
        };

        var member = new ProjectMember
        {
            ProjectId = project.Id,
            UserId = "user-123",
            ProjectRole = ProjectMemberRole.Manager,
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
        Assert.Equal("user-123", response.CreatedByUserId);
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
            CreatedByUserId = "owner-123"
        };

        _projects.AddedProjects.Add(project);

        _members.AddedMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = "member-123",
            ProjectRole = ProjectMemberRole.Member
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
            CreatedByUserId = "owner-123"
        };

        _projects.AddedProjects.Add(project);

        var service = CreateService();

        var result = await service.GetByIdAsync(
            project.Id,
            "stranger-123");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldAllowAdminWithoutMembership()
    {
        var project = new Project { Name = "Admin Project" };
        _projects.AddedProjects.Add(project);
        _users.Roles["admin-123"] = ["Admin"];

        var result = await CreateService().GetByIdAsync(
            project.Id, "admin-123");

        Assert.NotNull(result);
        Assert.Equal(project.Id, result.Id);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectTeamMember()
    {
        _users.Roles["member-123"] = ["Team Member"];

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            CreateService().CreateAsync(
                new CreateProjectRequest { Name = "Not Allowed" },
                "member-123"));

        Assert.Empty(_projects.AddedProjects);
        Assert.Empty(_members.AddedMembers);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task AddMemberAsync_ShouldAddUserAsProjectMember()
    {
        var project = new Project { Name = "Team Project" };
        _projects.AddedProjects.Add(project);
        _users.Roles["manager-123"] = ["Project Manager"];
        _members.AddedMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = "manager-123",
            ProjectRole = ProjectMemberRole.Manager
        });
        _users.Users["new-member"] = new UserAccount
        {
            Id = "new-member",
            DisplayName = "New Member",
            Email = "member@example.com"
        };

        var result = await CreateService().AddMemberAsync(
            project.Id,
            new AddProjectMemberRequest { UserId = "new-member" },
            "manager-123");

        Assert.NotNull(result);
        Assert.Equal("new-member", result.UserId);
        Assert.Equal("New Member", result.DisplayName);
        Assert.Equal("member@example.com", result.Email);
        Assert.Equal(ProjectMemberRole.Member, result.ProjectRole);

        Assert.Equal(2, _members.AddedMembers.Count);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task AddMemberAsync_ShouldRejectDuplicateMembership()
    {
        var project = new Project { Name = "Team Project" };
        _projects.AddedProjects.Add(project);
        _users.Roles["manager-123"] = ["Project Manager"];
        _members.AddedMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = "manager-123",
            ProjectRole = ProjectMemberRole.Manager
        });
        _members.AddedMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = "existing-member",
            ProjectRole = ProjectMemberRole.Member
        });
        _users.Users["existing-member"] = new UserAccount
        {
            Id = "existing-member",
            DisplayName = "Existing Member",
            Email = "existing@example.com"
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().AddMemberAsync(
                project.Id,
                new AddProjectMemberRequest { UserId = "existing-member" },
                "manager-123"));

        Assert.Equal(2, _members.AddedMembers.Count);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task AddMemberAsync_ShouldReturnNullForUnknownUser()
    {
        var project = new Project { Name = "Team Project" };
        _projects.AddedProjects.Add(project);
        _users.Roles["manager-123"] = ["Project Manager"];
        _members.AddedMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = "manager-123",
            ProjectRole = ProjectMemberRole.Manager
        });

        var result = await CreateService().AddMemberAsync(
            project.Id,
            new AddProjectMemberRequest { UserId = "unknown-user" },
            "manager-123");

        Assert.Null(result);
        Assert.Single(_members.AddedMembers);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task GetMembersAsync_ShouldReturnMemberProfilesAndRoles()
    {
        var project = new Project { Name = "Team Project" };
        _projects.AddedProjects.Add(project);
        _users.Roles["manager-123"] = ["Project Manager"];

        _members.AddedMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = "manager-123",
            ProjectRole = ProjectMemberRole.Manager
        });
        _members.AddedMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = "member-123",
            ProjectRole = ProjectMemberRole.Member
        });

        _users.Users["manager-123"] = new UserAccount
        {
            Id = "manager-123",
            DisplayName = "Project Manager",
            Email = "manager@example.com"
        };
        _users.Users["member-123"] = new UserAccount
        {
            Id = "member-123",
            DisplayName = "Team Member",
            Email = "member@example.com"
        };

        var result = await CreateService().GetMembersAsync(
            project.Id, "manager-123");

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, m =>
            m.UserId == "manager-123" &&
            m.ProjectRole == ProjectMemberRole.Manager);
        Assert.Contains(result, m =>
            m.UserId == "member-123" &&
            m.DisplayName == "Team Member" &&
            m.Email == "member@example.com" &&
            m.ProjectRole == ProjectMemberRole.Member);
    }

    [Fact]
    public async Task RemoveMemberAsync_ShouldRemoveMembership()
    {
        var project = new Project { Name = "Team Project" };
        _projects.AddedProjects.Add(project);
        _users.Roles["manager-123"] = ["Project Manager"];

        _members.AddedMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = "manager-123",
            ProjectRole = ProjectMemberRole.Manager
        });
        _members.AddedMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = "member-123",
            ProjectRole = ProjectMemberRole.Member
        });

        var result = await CreateService().RemoveMemberAsync(
            project.Id, "member-123", "manager-123");

        Assert.True(result);
        Assert.Single(_members.AddedMembers);
        Assert.Equal("manager-123", _members.AddedMembers[0].UserId);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task AddMemberAsync_ShouldRejectTeamMemberWithoutManagerPermission()
    {
        var project = new Project { Name = "Team Project" };
        _projects.AddedProjects.Add(project);
        _users.Roles["team-member"] = ["Team Member"];
        _users.Users["new-member"] = new UserAccount
        {
            Id = "new-member",
            DisplayName = "New Member",
            Email = "member@example.com"
        };

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            CreateService().AddMemberAsync(
                project.Id,
                new AddProjectMemberRequest { UserId = "new-member" },
                "team-member"));

        Assert.Empty(_members.AddedMembers);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
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

        public Task<IReadOnlyList<Project>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Project> result = AddedProjects
                .OrderByDescending(p => p.CreatedAtUtc)
                .ToList();

            return Task.FromResult(result);
        }

        public Task<(IReadOnlyList<Project> Items, int TotalCount)>
            GetPagedAllAsync(
                ProjectListQueryRequest query,
                CancellationToken cancellationToken = default)
        {
            IEnumerable<Project> projects = AddedProjects;

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

            IOrderedEnumerable<Project> ordered = query.SortBy.Trim().ToLowerInvariant() switch
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
            IReadOnlyList<Project> items = ordered
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
            CreatedByUserId = "owner-123"
        };

        _projects.AddedProjects.Add(project);
        _users.Roles["owner-123"] = ["Project Manager"];
        _members.AddedMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = "owner-123",
            ProjectRole = ProjectMemberRole.Manager
        });

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
            CreatedByUserId = "owner-123"
        };

        _projects.AddedProjects.Add(project);

        var service = CreateService();

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => service.UpdateAsync(
                project.Id,
                new UpdateProjectRequest { Name = "Changed Name" },
                "another-user"));
        Assert.Equal("Original Name", project.Name);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteProjectForOwner()
    {
        var project = new Project
        {
            Name = "Project to Delete",
            CreatedByUserId = "owner-123"
        };

        _projects.AddedProjects.Add(project);
        _users.Roles["owner-123"] = ["Project Manager"];
        _members.AddedMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = "owner-123",
            ProjectRole = ProjectMemberRole.Manager
        });

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
            CreatedByUserId = "owner-123"
        };

        _projects.AddedProjects.Add(project);

        var service = CreateService();

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => service.DeleteAsync(
                project.Id,
                "another-user"));
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

    private sealed class FakeUserAccountRepository : IUserAccountRepository
    {
        public Dictionary<string, IReadOnlyList<string>> Roles { get; } = [];
        public Dictionary<string, UserAccount> Users { get; } = [];

        public Task<IReadOnlyList<string>> GetRolesAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<string> roles = Roles.TryGetValue(userId, out var value)
                ? value
                : Array.Empty<string>();

            return Task.FromResult(roles);
        }

        public Task<UserAccount?> FindByIdAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            Users.TryGetValue(userId, out var user);
            return Task.FromResult(user);
        }

        public Task<(bool Succeeded, string UserId, IReadOnlyList<string> Errors)>
            CreateUserAsync(
                string displayName,
                string email,
                string password,
                CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<UserAccount?> FindByEmailAsync(
            string email,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<bool> CheckPasswordAsync(
            string userId,
            string password,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<(bool Succeeded, IReadOnlyList<string> Errors)> AddToRoleAsync(
            string userId,
            string role,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
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

            return Task.FromResult(2);
        }
    }
}

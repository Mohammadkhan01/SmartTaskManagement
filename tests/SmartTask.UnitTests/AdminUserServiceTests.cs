using FluentValidation;
using Moq;
using SmartTask.BLL.DTOs.Admin;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.BLL.Services.Admin;
using SmartTask.BLL.Validators.Admin;
using SmartTask.Common.Constants;
using SmartTask.Common.Exceptions;
using SmartTask.Domain.Entities;

namespace SmartTask.UnitTests;

public class AdminUserServiceTests
{
    private const string ActorId = "admin-1";
    private const string Ip = "192.0.2.44";

    private readonly Mock<IAdminUserRepository> _users = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly List<AuditLog> _audits = [];
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public AdminUserServiceTests()
    {
        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> operation, CancellationToken ct) =>
                operation(ct));

        _refreshTokens
            .Setup(r => r.GetActiveByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _users
            .Setup(r => r.SetActiveAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, Array.Empty<string>()));

        _users
            .Setup(r => r.ReplaceRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, Array.Empty<string>()));
    }

    private AdminUserService CreateService()
    {
        var auditRepository = new Mock<IAuditLogRepository>();
        auditRepository
            .Setup(a => a.Add(It.IsAny<AuditLog>()))
            .Callback<AuditLog>(_audits.Add);

        return new AdminUserService(
            _users.Object,
            _refreshTokens.Object,
            auditRepository.Object,
            _unitOfWork.Object,
            new AdminUserListQueryRequestValidator(),
            new UpdateUserRoleRequestValidator(),
            new UpdateUserStatusRequestValidator());
    }

    private void GivenUser(string id, bool isActive, params string[] roles)
    {
        _users
            .Setup(r => r.FindByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminUserAccount
            {
                Id = id,
                Email = $"{id}@example.com",
                DisplayName = id,
                IsActive = isActive,
                Roles = roles
            });
    }

    [Fact]
    public async Task UpdateStatusAsync_Deactivate_RevokesTokensAndAuditsWithIp()
    {
        GivenUser("member-1", true, RoleNames.TeamMember);
        var token = new RefreshToken { UserId = "member-1", TokenHash = "h" };
        _refreshTokens
            .Setup(r => r.GetActiveByUserIdAsync("member-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([token]);

        var result = await CreateService().UpdateStatusAsync(
            "member-1",
            new UpdateUserStatusRequest { IsActive = false },
            ActorId,
            Ip);

        Assert.NotNull(result);
        _users.Verify(r => r.SetActiveAsync("member-1", false, It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(token.RevokedAtUtc);
        Assert.Equal(Ip, token.RevokedByIp);

        var audit = Assert.Single(_audits);
        Assert.Equal("UserStatusChanged", audit.Action);
        Assert.Equal("User", audit.EntityName);
        Assert.Equal("member-1", audit.EntityId);
        Assert.Equal(ActorId, audit.UserId);
        Assert.Equal(Ip, audit.IpAddress);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_AdminCannotDeactivateSelf()
    {
        GivenUser(ActorId, true, RoleNames.Admin);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            CreateService().UpdateStatusAsync(
                ActorId,
                new UpdateUserStatusRequest { IsActive = false },
                ActorId));

        Assert.Empty(_audits);
    }

    [Fact]
    public async Task UpdateStatusAsync_LastActiveAdminCannotBeDeactivated()
    {
        GivenUser("admin-2", true, RoleNames.Admin);
        _users
            .Setup(r => r.CountActiveInRoleAsync(RoleNames.Admin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            CreateService().UpdateStatusAsync(
                "admin-2",
                new UpdateUserStatusRequest { IsActive = false },
                ActorId));

        _users.Verify(r => r.SetActiveAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_UnknownUser_ReturnsNull()
    {
        var result = await CreateService().UpdateStatusAsync(
            "missing",
            new UpdateUserStatusRequest { IsActive = true },
            ActorId);

        Assert.Null(result);
        Assert.Empty(_audits);
    }

    [Fact]
    public async Task UpdateRoleAsync_ChangesRoleCaseInsensitivelyAndAudits()
    {
        GivenUser("member-1", true, RoleNames.TeamMember);

        await CreateService().UpdateRoleAsync(
            "member-1",
            new UpdateUserRoleRequest { Role = "project manager" },
            ActorId,
            Ip);

        _users.Verify(r => r.ReplaceRoleAsync("member-1", RoleNames.ProjectManager, It.IsAny<CancellationToken>()), Times.Once);
        var audit = Assert.Single(_audits);
        Assert.Equal("UserRoleChanged", audit.Action);
        Assert.Equal(Ip, audit.IpAddress);
        Assert.Contains(RoleNames.ProjectManager, audit.Details);
    }

    [Fact]
    public async Task UpdateRoleAsync_AdminCannotRemoveOwnAdminRole()
    {
        GivenUser(ActorId, true, RoleNames.Admin);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            CreateService().UpdateRoleAsync(
                ActorId,
                new UpdateUserRoleRequest { Role = RoleNames.TeamMember },
                ActorId));
    }

    [Fact]
    public async Task UpdateRoleAsync_RejectsUnknownRole()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().UpdateRoleAsync(
                "member-1",
                new UpdateUserRoleRequest { Role = "Superuser" },
                ActorId));
    }

    [Fact]
    public async Task GetUsersAsync_NormalizesRoleFilterAndCalculatesPages()
    {
        AdminUserListQueryRequest? captured = null;
        _users
            .Setup(r => r.GetPagedAsync(It.IsAny<AdminUserListQueryRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AdminUserListQueryRequest, CancellationToken>((q, _) => captured = q)
            .ReturnsAsync((Array.Empty<AdminUserAccount>(), 41));

        var result = await CreateService().GetUsersAsync(new AdminUserListQueryRequest
        {
            Role = " team member ",
            PageSize = 20
        });

        Assert.Equal(RoleNames.TeamMember, captured?.Role);
        Assert.Equal(41, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }
}

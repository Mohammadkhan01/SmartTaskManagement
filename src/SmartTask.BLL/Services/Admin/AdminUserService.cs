using System.Text.Json;
using FluentValidation;
using SmartTask.BLL.DTOs.Admin;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.BLL.Interfaces.Services;
using SmartTask.Domain.Entities;
using SmartTask.Common.Constants;
using SmartTask.Common.Exceptions;
using SmartTask.Common.Utilities;

namespace SmartTask.BLL.Services.Admin;

public sealed class AdminUserService : IAdminUserService
{
    private static readonly string[] AllowedRoles =
    [
        RoleNames.Admin,
        RoleNames.ProjectManager,
        RoleNames.TeamMember
    ];

    private readonly IAdminUserRepository _adminUserRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<AdminUserListQueryRequest> _listValidator;
    private readonly IValidator<UpdateUserRoleRequest> _roleValidator;
    private readonly IValidator<UpdateUserStatusRequest> _statusValidator;

    public AdminUserService(
        IAdminUserRepository adminUserRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        IValidator<AdminUserListQueryRequest> listValidator,
        IValidator<UpdateUserRoleRequest> roleValidator,
        IValidator<UpdateUserStatusRequest> statusValidator)
    {
        _adminUserRepository = adminUserRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _listValidator = listValidator;
        _roleValidator = roleValidator;
        _statusValidator = statusValidator;
    }

    public async Task<AdminUserListResponse> GetUsersAsync(
        AdminUserListQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await _listValidator.ValidateAndThrowAsync(query, cancellationToken);

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            query.Role = AllowedRoles.Single(role =>
                string.Equals(
                    role,
                    query.Role.Trim(),
                    StringComparison.OrdinalIgnoreCase));
        }

        var (users, totalCount) = await _adminUserRepository.GetPagedAsync(
            query,
            cancellationToken);

        return new AdminUserListResponse
        {
            Items = users.Select(MapToResponse).ToArray(),
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalCount = totalCount,
            TotalPages = Pagination.CalculateTotalPages(
                totalCount,
                query.PageSize)
        };
    }

    public async Task<AdminUserResponse?> UpdateStatusAsync(
        string userId,
        UpdateUserStatusRequest request,
        string actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorUserId);

        await _statusValidator.ValidateAndThrowAsync(request, cancellationToken);
        var requestedIsActive = request.IsActive!.Value;

        AdminUserResponse? response = null;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var user = await _adminUserRepository.FindByIdAsync(userId, ct);

            if (user is null)
            {
                return;
            }

            if (user.IsActive == requestedIsActive)
            {
                response = MapToResponse(user);
                return;
            }

            if (!requestedIsActive &&
                string.Equals(userId, actorUserId, StringComparison.Ordinal))
            {
                throw new ForbiddenAccessException(
                    "Administrators cannot deactivate their own account.");
            }

            var isActiveAdmin = user.IsActive &&
                user.Roles.Contains(RoleNames.Admin, StringComparer.OrdinalIgnoreCase);

            if (!requestedIsActive && isActiveAdmin)
            {
                var activeAdminCount =
                    await _adminUserRepository.CountActiveInRoleAsync(RoleNames.Admin, ct);

                if (activeAdminCount <= 1)
                {
                    throw new ForbiddenAccessException(
                        "The last active administrator cannot be deactivated.");
                }
            }

            var updateResult = await _adminUserRepository.SetActiveAsync(
                userId,
                requestedIsActive,
                ct);

            if (!updateResult.Succeeded)
            {
                throw new ConflictException(
                    $"The user status could not be updated: {string.Join("; ", updateResult.Errors)}");
            }

            await RevokeActiveRefreshTokensAsync(userId, ipAddress, ct);

            _auditLogRepository.Add(new AuditLog
            {
                UserId = actorUserId,
                Action = "UserStatusChanged",
                EntityName = "User",
                EntityId = userId,
                IpAddress = ipAddress,
                Details = JsonSerializer.Serialize(new
                {
                    PreviousIsActive = user.IsActive,
                    NewIsActive = requestedIsActive
                })
            });

            await _unitOfWork.SaveChangesAsync(ct);

            var updatedUser = await _adminUserRepository.FindByIdAsync(userId, ct);
            response = updatedUser is null ? null : MapToResponse(updatedUser);
        }, cancellationToken);

        return response;
    }

    public async Task<AdminUserResponse?> UpdateRoleAsync(
        string userId,
        UpdateUserRoleRequest request,
        string actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorUserId);

        await _roleValidator.ValidateAndThrowAsync(request, cancellationToken);

        var requestedRole = AllowedRoles.Single(role =>
            string.Equals(
                role,
                request.Role.Trim(),
                StringComparison.OrdinalIgnoreCase));

        AdminUserResponse? response = null;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var user = await _adminUserRepository.FindByIdAsync(userId, ct);

            if (user is null)
            {
                return;
            }

            var isCurrentlyAdmin = user.Roles.Contains(
                RoleNames.Admin,
                StringComparer.OrdinalIgnoreCase);

            var alreadyHasRequestedRole = user.Roles.Count == 1 &&
                string.Equals(
                    user.Roles[0],
                    requestedRole,
                    StringComparison.OrdinalIgnoreCase);

            if (alreadyHasRequestedRole)
            {
                response = MapToResponse(user);
                return;
            }

            var isChangingOwnAdminRole =
                string.Equals(userId, actorUserId, StringComparison.Ordinal) &&
                isCurrentlyAdmin &&
                !string.Equals(
                    requestedRole,
                    RoleNames.Admin,
                    StringComparison.OrdinalIgnoreCase);

            if (isChangingOwnAdminRole)
            {
                throw new ForbiddenAccessException(
                    "Administrators cannot remove their own Admin role.");
            }

            if (user.IsActive && isCurrentlyAdmin &&
                !string.Equals(
                    requestedRole,
                    RoleNames.Admin,
                    StringComparison.OrdinalIgnoreCase))
            {
                var activeAdminCount =
                    await _adminUserRepository.CountActiveInRoleAsync(RoleNames.Admin, ct);

                if (activeAdminCount <= 1)
                {
                    throw new ForbiddenAccessException(
                        "The last active administrator cannot be demoted.");
                }
            }

            var previousRoles = user.Roles.ToArray();

            var updateResult = await _adminUserRepository.ReplaceRoleAsync(
                userId,
                requestedRole,
                ct);

            if (!updateResult.Succeeded)
            {
                throw new ConflictException(
                    $"The user role could not be updated: {string.Join("; ", updateResult.Errors)}");
            }

            await RevokeActiveRefreshTokensAsync(userId, ipAddress, ct);

            _auditLogRepository.Add(new AuditLog
            {
                UserId = actorUserId,
                Action = "UserRoleChanged",
                EntityName = "User",
                EntityId = userId,
                IpAddress = ipAddress,
                Details = JsonSerializer.Serialize(new
                {
                    PreviousRoles = previousRoles,
                    NewRole = requestedRole
                })
            });

            await _unitOfWork.SaveChangesAsync(ct);

            var updatedUser = await _adminUserRepository.FindByIdAsync(userId, ct);
            response = updatedUser is null ? null : MapToResponse(updatedUser);
        }, cancellationToken);

        return response;
    }

    private async Task RevokeActiveRefreshTokensAsync(
        string userId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var activeTokens = await _refreshTokenRepository.GetActiveByUserIdAsync(
            userId,
            cancellationToken);

        var revokedAtUtc = DateTime.UtcNow;

        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = revokedAtUtc;
            token.RevokedByIp = ipAddress;
        }
    }

    private static AdminUserResponse MapToResponse(AdminUserAccount user)
    {
        return new AdminUserResponse
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
            IsActive = user.IsActive,
            Roles = user.Roles.ToArray()
        };
    }
}

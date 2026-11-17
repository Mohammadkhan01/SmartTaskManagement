using SmartTask.BLL.DTOs.Admin;

namespace SmartTask.BLL.Interfaces.Services;

public interface IAdminUserService
{
    Task<AdminUserListResponse> GetUsersAsync(
        AdminUserListQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<AdminUserResponse?> UpdateStatusAsync(
        string userId,
        UpdateUserStatusRequest request,
        string actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task<AdminUserResponse?> UpdateRoleAsync(
        string userId,
        UpdateUserRoleRequest request,
        string actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);
}

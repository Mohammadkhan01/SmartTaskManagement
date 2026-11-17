using SmartTask.BLL.DTOs.Admin;

namespace SmartTask.BLL.Interfaces.Repositories;

public interface IAdminUserRepository
{
    Task<(IReadOnlyList<AdminUserAccount> Items, int TotalCount)> GetPagedAsync(
        AdminUserListQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<AdminUserAccount?> FindByIdAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveInRoleAsync(
        string role,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, IReadOnlyList<string> Errors)> SetActiveAsync(
        string userId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, IReadOnlyList<string> Errors)> ReplaceRoleAsync(
        string userId,
        string role,
        CancellationToken cancellationToken = default);
}

public sealed class AdminUserAccount
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}

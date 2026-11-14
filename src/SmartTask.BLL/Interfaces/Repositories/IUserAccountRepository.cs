namespace SmartTask.BLL.Interfaces.Repositories;

public interface IUserAccountRepository
{
    Task<(bool Succeeded, string UserId, IReadOnlyList<string> Errors)>
        CreateUserAsync(
            string displayName,
            string email,
            string password,
            CancellationToken cancellationToken = default);

    Task<UserAccount?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<bool> CheckPasswordAsync(
        string userId,
        string password,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetRolesAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, IReadOnlyList<string> Errors)> AddToRoleAsync(
        string userId,
        string role,
        CancellationToken cancellationToken = default);
}

public sealed class UserAccount
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

using SmartTask.BLL.DTOs.Auth;

namespace SmartTask.BLL.Interfaces.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default,
        string? ipAddress = null);

    Task<AuthResponse> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default,
        string? ipAddress = null);

    Task LogoutAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default,
        string? ipAddress = null);

    Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default,
        string? ipAddress = null);
}

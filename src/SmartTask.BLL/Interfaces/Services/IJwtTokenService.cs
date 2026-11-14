using SmartTask.BLL.DTOs.Auth;

namespace SmartTask.BLL.Interfaces.Services;

public interface IJwtTokenService
{
    AccessTokenResponse GenerateAccessToken(
        string userId,
        string email,
        IEnumerable<string> roles);
}

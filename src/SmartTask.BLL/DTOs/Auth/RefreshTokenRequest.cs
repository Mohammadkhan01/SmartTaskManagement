namespace SmartTask.BLL.DTOs.Auth;

public sealed class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

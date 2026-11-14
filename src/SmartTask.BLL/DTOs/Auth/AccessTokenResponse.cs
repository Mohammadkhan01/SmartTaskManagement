namespace SmartTask.BLL.DTOs.Auth;

public sealed class AccessTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
}

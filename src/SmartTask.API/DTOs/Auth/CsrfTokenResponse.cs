namespace SmartTask.API.DTOs.Auth;

public sealed class CsrfTokenResponse
{
    public string RequestToken { get; set; } = string.Empty;
}

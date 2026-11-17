namespace SmartTask.API.DTOs.Auth;

public sealed class CurrentUserResponse
{
    public string UserId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = [];
}

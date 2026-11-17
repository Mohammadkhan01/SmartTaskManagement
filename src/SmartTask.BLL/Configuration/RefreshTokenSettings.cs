namespace SmartTask.BLL.Configuration;

public sealed class RefreshTokenSettings
{
    public const string SectionName = "RefreshToken";

    public int ExpirationDays { get; set; } = 7;
}

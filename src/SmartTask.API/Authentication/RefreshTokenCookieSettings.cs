using Microsoft.AspNetCore.Http;

namespace SmartTask.API.Authentication;

public sealed class RefreshTokenCookieSettings
{
    public const string SectionName = "RefreshTokenCookie";

    public string Name { get; set; } = "smarttask.refresh";

    // Must cover the versioned /api/v1/auth routes.
    public string Path { get; set; } = "/api/";

    public SameSiteMode SameSite { get; set; } = SameSiteMode.Lax;

    public CookieSecurePolicy SecurePolicy { get; set; } =
        CookieSecurePolicy.Always;

    public static bool IsValid(
        RefreshTokenCookieSettings settings,
        out string error)
    {
        if (string.IsNullOrWhiteSpace(settings.Name) ||
            settings.Name.Any(char.IsWhiteSpace) ||
            settings.Name.Contains(';'))
        {
            error = "Refresh token cookie name is invalid.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(settings.Path) ||
            !settings.Path.StartsWith("/api/", StringComparison.Ordinal) ||
            settings.Path.Contains(';') ||
            settings.Path.Contains("..", StringComparison.Ordinal))
        {
            error = "Refresh token cookie path must be a safe /api/ path.";
            return false;
        }

        if (settings.SameSite is not (
            SameSiteMode.Lax or
            SameSiteMode.Strict or
            SameSiteMode.None))
        {
            error = "Refresh token cookie SameSite setting is invalid.";
            return false;
        }

        if (settings.SecurePolicy != CookieSecurePolicy.Always &&
            settings.SecurePolicy != CookieSecurePolicy.SameAsRequest)
        {
            error = "Refresh token cookies must use a secure policy.";
            return false;
        }

        if (settings.SameSite == SameSiteMode.None &&
            settings.SecurePolicy != CookieSecurePolicy.Always)
        {
            error = "SameSite=None requires SecurePolicy=Always.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

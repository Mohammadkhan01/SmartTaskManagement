
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartTask.BLL.Configuration;
using SmartTask.BLL.Interfaces.Repositories;
using Microsoft.IdentityModel.Tokens;
using SmartTask.BLL.Interfaces.Services;

namespace SmartTask.API.Authentication;

public static class AuthenticationDependencyInjection
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = configuration
            .GetSection(JwtSettings.SectionName)
            .Get<JwtSettings>()
            ?? throw new InvalidOperationException(
                "JWT configuration section is missing.");

        if (string.IsNullOrWhiteSpace(jwtSettings.Issuer) ||
            string.IsNullOrWhiteSpace(jwtSettings.Audience) ||
            string.IsNullOrWhiteSpace(jwtSettings.SigningKey))
        {
            throw new InvalidOperationException(
                "JWT issuer, audience, and signing key must be configured.");
        }

        if (Encoding.UTF8.GetByteCount(jwtSettings.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "JWT signing key must be at least 32 bytes.");
        }

        if (jwtSettings.AccessTokenExpirationMinutes <= 0)
        {
            throw new InvalidOperationException(
                "JWT access token expiration must be greater than zero.");
        }

        var refreshTokenSettings = configuration
            .GetSection(RefreshTokenSettings.SectionName)
            .Get<RefreshTokenSettings>()
            ?? new RefreshTokenSettings();

        if (refreshTokenSettings.ExpirationDays <= 0)
        {
            throw new InvalidOperationException(
                "Refresh token expiration must be greater than zero.");
        }

        services.Configure<RefreshTokenSettings>(
            configuration.GetSection(RefreshTokenSettings.SectionName));

        services.AddOptions<RefreshTokenCookieSettings>()
            .Bind(configuration.GetSection(RefreshTokenCookieSettings.SectionName))
            .Validate(
                settings => RefreshTokenCookieSettings.IsValid(settings, out _),
                "Refresh token cookie configuration is invalid.")
            .ValidateOnStart();

        services.Configure<JwtSettings>(
            configuration.GetSection(JwtSettings.SectionName));

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme =
                    JwtBearerDefaults.AuthenticationScheme;

                options.DefaultChallengeScheme =
                    JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var userId = context.Principal?
                            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

                        if (string.IsNullOrWhiteSpace(userId))
                        {
                            context.Fail("The token does not contain a valid user identifier.");
                            return;
                        }

                        var userRepository = context.HttpContext.RequestServices
                            .GetRequiredService<IUserAccountRepository>();

                        var user = await userRepository.FindByIdAsync(
                            userId,
                            context.HttpContext.RequestAborted);

                        if (user is null || !user.IsActive)
                        {
                            context.Fail("The user account is inactive or no longer exists.");
                            return;
                        }

                        var tokenRoles = context.Principal?
                            .FindAll(ClaimTypes.Role)
                            .Select(claim => claim.Value)
                            .Where(role => !string.IsNullOrWhiteSpace(role))
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(role => role, StringComparer.Ordinal)
                            .ToArray()
                            ?? Array.Empty<string>();

                        var currentRoles = (await userRepository.GetRolesAsync(
                                userId,
                                context.HttpContext.RequestAborted))
                            .Where(role => !string.IsNullOrWhiteSpace(role))
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(role => role, StringComparer.Ordinal)
                            .ToArray();

                        if (!tokenRoles.SequenceEqual(
                                currentRoles,
                                StringComparer.Ordinal))
                        {
                            context.Fail("The user's roles have changed. Please authenticate again.");
                        }
                    }
                };
            });

        services.AddOptions<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme)
            .Configure<Microsoft.Extensions.Options.IOptions<JwtSettings>>(
                (options, settingsOptions) =>
                {
                    var settings = settingsOptions.Value;

                    options.TokenValidationParameters =
                        new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidIssuer = settings.Issuer,
                            ValidateAudience = true,
                            ValidAudience = settings.Audience,
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(settings.SigningKey)),
                            ValidateLifetime = true,
                            ClockSkew = TimeSpan.FromSeconds(30)
                        };
                });

        services.AddAuthorization();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        return services;
    }
}

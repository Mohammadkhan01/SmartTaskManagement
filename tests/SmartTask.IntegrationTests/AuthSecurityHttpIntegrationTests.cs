using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartTask.DAL.Identity;
using SmartTask.DAL.Persistence;

namespace SmartTask.IntegrationTests;

public sealed class AuthSecurityHttpIntegrationTests
{
    private const string CsrfCookieName = "smarttask.csrf";
    private const string RefreshCookieName = "smarttask.refresh";
    private const string TestIssuer = "SmartTaskIntegrationTests";
    private const string TestSigningKey =
        "IntegrationTestSigningKeyMustBeAtLeast32Chars";

    [SqlServerFact]
    public async Task ProtectedEndpoint_WithExpiredJwt_Returns401()
    {
        var connectionString = GetConnectionString();
        var options = CreateDbOptions(connectionString);
        var userId = Guid.NewGuid().ToString();
        var email = $"expired-jwt-{Guid.NewGuid():N}@example.com";

        await using var factory =
            new InactiveUserJwtWebApplicationFactory(connectionString);

        using var client = CreateClient(factory);

        try
        {
            await using (var setupContext = new SmartTaskDbContext(options))
            {
                setupContext.Users.Add(new ApplicationUser
                {
                    Id = userId,
                    UserName = email,
                    NormalizedUserName = email.ToUpperInvariant(),
                    Email = email,
                    NormalizedEmail = email.ToUpperInvariant(),
                    SecurityStamp = Guid.NewGuid().ToString(),
                    ConcurrencyStamp = Guid.NewGuid().ToString(),
                    IsActive = true
                });

                await setupContext.SaveChangesAsync();
            }

            // Control: an unexpired token for the same user is accepted.
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    CreateToken(userId, email, DateTime.UtcNow.AddMinutes(10)));

            var validResponse = await client.GetAsync("/api/v1/auth/me");

            Assert.Equal(HttpStatusCode.OK, validResponse.StatusCode);

            // Expired beyond the configured 30-second clock skew.
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    CreateToken(userId, email, DateTime.UtcNow.AddMinutes(-5)));

            var expiredResponse = await client.GetAsync("/api/v1/auth/me");

            Assert.Equal(HttpStatusCode.Unauthorized, expiredResponse.StatusCode);
            Assert.Contains(
                expiredResponse.Headers.WwwAuthenticate,
                header => header.Parameter?.Contains(
                    "invalid_token",
                    StringComparison.Ordinal) == true);
        }
        finally
        {
            await CleanupUserAsync(options, userId);
        }
    }

    [SqlServerFact]
    public async Task ProtectedEndpoint_WithTamperedJwtSignature_Returns401()
    {
        var connectionString = GetConnectionString();

        await using var factory =
            new InactiveUserJwtWebApplicationFactory(connectionString);

        using var client = CreateClient(factory);

        var token = CreateToken(
            Guid.NewGuid().ToString(),
            "tampered@example.com",
            DateTime.UtcNow.AddMinutes(10),
            signingKey: "AnotherSigningKeyThatIsAtLeast32CharsLong");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SqlServerFact]
    public async Task Refresh_WithRotatedToken_Returns401AndRevokesTokenFamily()
    {
        var connectionString = GetConnectionString();
        var options = CreateDbOptions(connectionString);
        var email = $"refresh-rotation-{Guid.NewGuid():N}@example.com";
        string? userId = null;

        await using var factory =
            new InactiveUserJwtWebApplicationFactory(connectionString);

        using var client = CreateClient(factory);

        try
        {
            var csrf = await GetCsrfAsync(client);

            var registerResponse = await SendAuthAsync(
                client,
                "/api/v1/auth/register",
                csrf,
                refreshToken: null,
                body: new
                {
                    displayName = "Refresh Rotation",
                    email,
                    password = "Rotation-Test-Password-1!"
                });

            Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

            userId = await ReadUserIdAsync(registerResponse);
            var originalRefreshToken = GetRefreshCookie(registerResponse);

            // A valid refresh rotates the token and issues a new cookie.
            var refreshResponse = await SendAuthAsync(
                client,
                "/api/v1/auth/refresh",
                csrf,
                originalRefreshToken);

            Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

            var rotatedRefreshToken = GetRefreshCookie(refreshResponse);

            Assert.NotEqual(originalRefreshToken, rotatedRefreshToken);

            // Replaying the rotated-out token is rejected.
            var replayResponse = await SendAuthAsync(
                client,
                "/api/v1/auth/refresh",
                csrf,
                originalRefreshToken);

            Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);

            // Reuse detection revokes the replacement token as well.
            var familyRevokedResponse = await SendAuthAsync(
                client,
                "/api/v1/auth/refresh",
                csrf,
                rotatedRefreshToken);

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                familyRevokedResponse.StatusCode);

            await using var verificationContext =
                new SmartTaskDbContext(options);

            var tokens = await verificationContext.RefreshTokens
                .AsNoTracking()
                .Where(token => token.UserId == userId)
                .ToListAsync();

            Assert.Equal(2, tokens.Count);
            Assert.All(tokens, token => Assert.NotNull(token.RevokedAtUtc));
        }
        finally
        {
            if (userId is not null)
            {
                await CleanupUserAsync(options, userId);
            }
        }
    }

    [SqlServerFact]
    public async Task Refresh_AfterLogout_RejectsRevokedToken()
    {
        var connectionString = GetConnectionString();
        var options = CreateDbOptions(connectionString);
        var email = $"refresh-logout-{Guid.NewGuid():N}@example.com";
        string? userId = null;

        await using var factory =
            new InactiveUserJwtWebApplicationFactory(connectionString);

        using var client = CreateClient(factory);

        try
        {
            var csrf = await GetCsrfAsync(client);

            var registerResponse = await SendAuthAsync(
                client,
                "/api/v1/auth/register",
                csrf,
                refreshToken: null,
                body: new
                {
                    displayName = "Refresh Logout",
                    email,
                    password = "Logout-Test-Password-1!"
                });

            Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

            userId = await ReadUserIdAsync(registerResponse);
            var refreshToken = GetRefreshCookie(registerResponse);

            var logoutResponse = await SendAuthAsync(
                client,
                "/api/v1/auth/logout",
                csrf,
                refreshToken);

            Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

            var refreshResponse = await SendAuthAsync(
                client,
                "/api/v1/auth/refresh",
                csrf,
                refreshToken);

            Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
            Assert.Null(GetCookieValue(refreshResponse, RefreshCookieName));
        }
        finally
        {
            if (userId is not null)
            {
                await CleanupUserAsync(options, userId);
            }
        }
    }

    private static string GetConnectionString()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "SMARTTASK_TEST_CONNECTION_STRING")!;

        Assert.Equal(
            "SmartTaskTestDb",
            new SqlConnectionStringBuilder(connectionString).InitialCatalog);

        return connectionString;
    }

    private static DbContextOptions<SmartTaskDbContext> CreateDbOptions(
        string connectionString)
    {
        return new DbContextOptionsBuilder<SmartTaskDbContext>()
            .UseSqlServer(connectionString)
            .Options;
    }

    // Cookies are managed manually so rotated-out tokens can be replayed.
    private static HttpClient CreateClient(
        InactiveUserJwtWebApplicationFactory factory)
    {
        return factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = false
            });
    }

    private static string CreateToken(
        string userId,
        string email,
        DateTime expiresAtUtc,
        string signingKey = TestSigningKey)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestIssuer,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Email, email)
            ],
            notBefore: expiresAtUtc.AddMinutes(-15),
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<CsrfTokens> GetCsrfAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/auth/csrf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var requestToken = document.RootElement
            .GetProperty("requestToken")
            .GetString()!;

        return new CsrfTokens(
            GetCookieValue(response, CsrfCookieName)!,
            requestToken);
    }

    private static async Task<HttpResponseMessage> SendAuthAsync(
        HttpClient client,
        string path,
        CsrfTokens csrf,
        string? refreshToken,
        object? body = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = body is null ? null : JsonContent.Create(body)
        };

        request.Headers.Add("X-CSRF-TOKEN", csrf.RequestToken);

        var cookieHeader = $"{CsrfCookieName}={csrf.CookieValue}";

        if (refreshToken is not null)
        {
            cookieHeader += $"; {RefreshCookieName}={refreshToken}";
        }

        request.Headers.Add("Cookie", cookieHeader);

        return await client.SendAsync(request);
    }

    private static string GetRefreshCookie(HttpResponseMessage response)
    {
        var value = GetCookieValue(response, RefreshCookieName);

        Assert.False(string.IsNullOrWhiteSpace(value));

        return value!;
    }

    private static string? GetCookieValue(
        HttpResponseMessage response,
        string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        var prefix = $"{cookieName}=";

        return cookies
            .Where(cookie => cookie.StartsWith(prefix, StringComparison.Ordinal))
            .Select(cookie => cookie[prefix.Length..].Split(';')[0])
            .FirstOrDefault();
    }

    private static async Task<string> ReadUserIdAsync(
        HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("userId").GetString()!;
    }

    private static async Task CleanupUserAsync(
        DbContextOptions<SmartTaskDbContext> options,
        string userId)
    {
        await using var context = new SmartTaskDbContext(options);

        var tokens = await context.RefreshTokens
            .Where(token => token.UserId == userId)
            .ToListAsync();

        // Break the ReplacedByTokenId self-reference before deleting.
        foreach (var token in tokens)
        {
            token.ReplacedByTokenId = null;
        }

        await context.SaveChangesAsync();

        context.RefreshTokens.RemoveRange(tokens);

        context.UserRoles.RemoveRange(
            await context.UserRoles
                .Where(userRole => userRole.UserId == userId)
                .ToListAsync());

        var user = await context.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == userId);

        if (user is not null)
        {
            context.Users.Remove(user);
        }

        await context.SaveChangesAsync();
    }

    private sealed record CsrfTokens(string CookieValue, string RequestToken);
}

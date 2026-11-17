using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SmartTask.API.Authentication;
using SmartTask.API.DTOs.Auth;
using SmartTask.BLL.DTOs.Auth;
using SmartTask.BLL.Interfaces.Services;

namespace SmartTask.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IAntiforgery _antiforgery;
    private readonly RefreshTokenCookieSettings _cookieSettings;

    public AuthController(
        IAuthService authService,
        IAntiforgery antiforgery,
        IOptions<RefreshTokenCookieSettings> cookieSettings)
    {
        _authService = authService;
        _antiforgery = antiforgery;
        _cookieSettings = cookieSettings.Value;
    }

    [HttpGet("csrf")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetCsrfToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { RequestToken = tokens.RequestToken });
    }

    [HttpPost("register")]
    [EnableRateLimiting("Auth")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(AuthSessionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthSessionResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.RegisterAsync(
            request,
            cancellationToken,
            HttpContext.Connection.RemoteIpAddress?.ToString());

        SetRefreshTokenCookie(response);

        return StatusCode(
            StatusCodes.Status201Created,
            ToSessionResponse(response));
    }

    [HttpPost("login")]
    [EnableRateLimiting("Auth")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(AuthSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthSessionResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.LoginAsync(
            request,
            cancellationToken,
            HttpContext.Connection.RemoteIpAddress?.ToString());

        SetRefreshTokenCookie(response);
        return Ok(ToSessionResponse(response));
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("Auth")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(AuthSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthSessionResponse>> Refresh(
        CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[_cookieSettings.Name];

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedAccessException();
        }

        var response = await _authService.RefreshAsync(
            new RefreshTokenRequest { RefreshToken = refreshToken },
            cancellationToken,
            HttpContext.Connection.RemoteIpAddress?.ToString());

        SetRefreshTokenCookie(response);
        return Ok(ToSessionResponse(response));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(
        CancellationToken cancellationToken)
    {
        try
        {
            var refreshToken = Request.Cookies[_cookieSettings.Name];

            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                await _authService.LogoutAsync(
                    new RefreshTokenRequest { RefreshToken = refreshToken },
                    cancellationToken,
                    HttpContext.Connection.RemoteIpAddress?.ToString());
            }
        }
        finally
        {
            ClearRefreshTokenCookie();
        }

        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);
        var roles = User.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToArray();

        return Ok(new
        {
            UserId = userId,
            Email = email,
            Roles = roles
        });
    }

    private void SetRefreshTokenCookie(AuthResponse response)
    {
        Response.Cookies.Append(
            _cookieSettings.Name,
            response.RefreshToken,
            CreateRefreshTokenCookieOptions(response.RefreshTokenExpiresAtUtc));
    }

    private void ClearRefreshTokenCookie()
    {
        Response.Cookies.Delete(
            _cookieSettings.Name,
            CreateRefreshTokenCookieOptions(DateTimeOffset.UnixEpoch));
    }

    private CookieOptions CreateRefreshTokenCookieOptions(DateTimeOffset expires)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = _cookieSettings.SecurePolicy == CookieSecurePolicy.Always ||
                     Request.IsHttps,
            SameSite = _cookieSettings.SameSite,
            Path = _cookieSettings.Path,
            Expires = expires,
            IsEssential = true
        };
    }

    private static AuthSessionResponse ToSessionResponse(AuthResponse response)
    {
        return new AuthSessionResponse
        {
            UserId = response.UserId,
            DisplayName = response.DisplayName,
            Email = response.Email,
            Roles = response.Roles,
            AccessToken = response.AccessToken,
            ExpiresAtUtc = response.ExpiresAtUtc
        };
    }
}

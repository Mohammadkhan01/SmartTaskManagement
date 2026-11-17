using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.Extensions.Options;
using SmartTask.BLL.Configuration;
using SmartTask.BLL.DTOs.Auth;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.BLL.Interfaces.Services;
using SmartTask.Domain.Entities;

namespace SmartTask.BLL.Services.Auth;

public sealed class AuthService : IAuthService
{
    private const string DefaultRole = "Team Member";

    private readonly IUserAccountRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RefreshTokenRequest> _refreshTokenValidator;
    private readonly RefreshTokenSettings _refreshTokenSettings;

    public AuthService(
        IUserAccountRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IJwtTokenService jwtTokenService,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<RefreshTokenRequest> refreshTokenValidator,
        IOptions<RefreshTokenSettings> refreshTokenOptions)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _jwtTokenService = jwtTokenService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _refreshTokenValidator = refreshTokenValidator;
        _refreshTokenSettings = refreshTokenOptions.Value;
    }

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default,
        string? ipAddress = null)
    {
        var validationResult = await _registerValidator.ValidateAsync(
            request,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var displayName = request.DisplayName.Trim();

        var createResult = await _userRepository.CreateUserAsync(
            displayName,
            email,
            request.Password,
            cancellationToken);

        if (!createResult.Succeeded)
        {
            var errors = createResult.Errors
                .Select(error => new FluentValidation.Results.ValidationFailure(
                    "Registration",
                    error))
                .ToList();

            throw new ValidationException(errors);
        }

        var roleResult = await _userRepository.AddToRoleAsync(
            createResult.UserId,
            DefaultRole,
            cancellationToken);

        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"User was created, but the default role could not be assigned: " +
                string.Join(" ", roleResult.Errors));
        }

        var roles = await _userRepository.GetRolesAsync(
            createResult.UserId,
            cancellationToken);

        var accessToken = _jwtTokenService.GenerateAccessToken(
            createResult.UserId,
            email,
            roles);

        var refreshToken = await CreateRefreshTokenAsync(
            createResult.UserId,
            ipAddress,
            cancellationToken);

        return new AuthResponse
        {
            UserId = createResult.UserId,
            DisplayName = displayName,
            Email = email,
            Roles = roles,
            AccessToken = accessToken.AccessToken,
            ExpiresAtUtc = accessToken.ExpiresAtUtc,
            RefreshToken = refreshToken.Token,
            RefreshTokenExpiresAtUtc = refreshToken.ExpiresAtUtc
        };
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default,
        string? ipAddress = null)
    {
        var validationResult = await _loginValidator.ValidateAsync(
            request,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _userRepository.FindByEmailAsync(
            email,
            cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        var passwordIsValid = await _userRepository.CheckPasswordAsync(
            user.Id,
            request.Password,
            cancellationToken);

        if (!passwordIsValid)
        {
            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        var roles = await _userRepository.GetRolesAsync(
            user.Id,
            cancellationToken);

        var accessToken = _jwtTokenService.GenerateAccessToken(
            user.Id,
            user.Email,
            roles);

        var refreshToken = await CreateRefreshTokenAsync(
            user.Id,
            ipAddress,
            cancellationToken);

        return new AuthResponse
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Roles = roles,
            AccessToken = accessToken.AccessToken,
            ExpiresAtUtc = accessToken.ExpiresAtUtc,
            RefreshToken = refreshToken.Token,
            RefreshTokenExpiresAtUtc = refreshToken.ExpiresAtUtc
        };
    }

    public async Task<AuthResponse> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default,
        string? ipAddress = null)
    {
        var validationResult = await _refreshTokenValidator.ValidateAsync(
            request,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var tokenHash = HashToken(request.RefreshToken);
        AuthResponse? response = null;
        var reuseDetected = false;

        await _unitOfWork.ExecuteInTransactionAsync(
            async transactionCancellationToken =>
            {
                var existingToken =
                    await _refreshTokenRepository.GetByTokenHashAsync(
                        tokenHash,
                        transactionCancellationToken);

                if (existingToken is null)
                {
                    return;
                }

                var now = DateTime.UtcNow;

                if (existingToken.RevokedAtUtc.HasValue)
                {
                    if (existingToken.ReplacedByTokenId.HasValue)
                    {
                        var activeTokens =
                            await _refreshTokenRepository.GetActiveByUserIdAsync(
                                existingToken.UserId,
                                transactionCancellationToken);

                        foreach (var activeToken in activeTokens)
                        {
                            activeToken.RevokedAtUtc = now;
                            activeToken.RevokedByIp = ipAddress;
                        }

                        if (activeTokens.Count > 0)
                        {
                            await _unitOfWork.SaveChangesAsync(
                                transactionCancellationToken);
                        }

                        reuseDetected = true;
                    }

                    return;
                }

                if (existingToken.ExpiresAtUtc <= now)
                {
                    return;
                }

                var user = await _userRepository.FindByIdAsync(
                    existingToken.UserId,
                    transactionCancellationToken);

                if (user is null || !user.IsActive)
                {
                    existingToken.RevokedAtUtc = now;
                    existingToken.RevokedByIp = ipAddress;

                    await _unitOfWork.SaveChangesAsync(
                        transactionCancellationToken);

                    return;
                }

                var roles = await _userRepository.GetRolesAsync(
                    user.Id,
                    transactionCancellationToken);

                var accessToken = _jwtTokenService.GenerateAccessToken(
                    user.Id,
                    user.Email,
                    roles);

                var replacement = BuildRefreshToken(user.Id, ipAddress);

                existingToken.RevokedAtUtc = now;
                existingToken.RevokedByIp = ipAddress;
                existingToken.ReplacedByTokenId = replacement.Entity.Id;

                _refreshTokenRepository.Add(replacement.Entity);

                await _unitOfWork.SaveChangesAsync(
                    transactionCancellationToken);

                response = new AuthResponse
                {
                    UserId = user.Id,
                    DisplayName = user.DisplayName,
                    Email = user.Email,
                    Roles = roles,
                    AccessToken = accessToken.AccessToken,
                    ExpiresAtUtc = accessToken.ExpiresAtUtc,
                    RefreshToken = replacement.Token,
                    RefreshTokenExpiresAtUtc = replacement.Entity.ExpiresAtUtc
                };
            },
            cancellationToken);

        // Throw after commit so reuse-triggered revocations are not rolled back.
        if (reuseDetected || response is null)
        {
            throw new UnauthorizedAccessException(
                "Invalid or expired authentication credentials.");
        }

        return response;
    }

    public async Task LogoutAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default,
        string? ipAddress = null)
    {
        var validationResult = await _refreshTokenValidator.ValidateAsync(
            request,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var tokenHash = HashToken(request.RefreshToken);

        await _unitOfWork.ExecuteInTransactionAsync(
            async transactionCancellationToken =>
            {
                var refreshToken =
                    await _refreshTokenRepository.GetByTokenHashAsync(
                        tokenHash,
                        transactionCancellationToken);

                // Unknown and already-revoked tokens are treated identically.
                if (refreshToken is null ||
                    refreshToken.RevokedAtUtc.HasValue)
                {
                    return;
                }

                refreshToken.RevokedAtUtc = DateTime.UtcNow;
                refreshToken.RevokedByIp = ipAddress;

                await _unitOfWork.SaveChangesAsync(
                    transactionCancellationToken);
            },
            cancellationToken);
    }

    private async Task<(string Token, DateTime ExpiresAtUtc)> CreateRefreshTokenAsync(
        string userId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var refreshToken = BuildRefreshToken(userId, ipAddress);

        _refreshTokenRepository.Add(refreshToken.Entity);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (refreshToken.Token, refreshToken.Entity.ExpiresAtUtc);
    }

    private (RefreshToken Entity, string Token) BuildRefreshToken(
        string userId,
        string? ipAddress)
    {
        var rawToken = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(64));

        var refreshToken = new RefreshToken
        {
            UserId = userId,
            TokenHash = HashToken(rawToken),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(
                _refreshTokenSettings.ExpirationDays),
            CreatedByIp = ipAddress
        };

        return (refreshToken, rawToken);
    }

    private static string HashToken(string rawToken)
    {
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }
}

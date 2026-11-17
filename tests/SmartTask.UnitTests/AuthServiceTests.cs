using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.Extensions.Options;
using SmartTask.BLL.Configuration;
using SmartTask.Domain.Entities;
using Moq;
using SmartTask.BLL.DTOs.Auth;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.BLL.Interfaces.Services;
using SmartTask.BLL.Services.Auth;
using SmartTask.BLL.Validators.Auth;

namespace SmartTask.UnitTests;

public sealed class AuthServiceTests
{
    private readonly Mock<IUserAccountRepository> _userRepository = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IJwtTokenService> _jwtTokenService = new();

    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _unitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _unitOfWork
            .Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                Func<CancellationToken, Task> operation,
                CancellationToken cancellationToken) =>
                    operation(cancellationToken));

        _service = new AuthService(
            _userRepository.Object,
            _refreshTokenRepository.Object,
            _unitOfWork.Object,
            _jwtTokenService.Object,
            new RegisterRequestValidator(),
            new LoginRequestValidator(),
            new RefreshTokenRequestValidator(),
            Options.Create(new RefreshTokenSettings
            {
                ExpirationDays = 7
            }));
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_CreatesUserAssignsRoleAndReturnsToken()
    {
        // Arrange
        var request = new RegisterRequest
        {
            DisplayName = " Test User ",
            Email = " TEST@EXAMPLE.COM ",
            Password = "StrongPassword123!"
        };

        _userRepository
            .Setup(x => x.CreateUserAsync(
                "Test User",
                "test@example.com",
                request.Password,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, "user-123", (IReadOnlyList<string>)Array.Empty<string>()));

        _userRepository
            .Setup(x => x.AddToRoleAsync(
                "user-123",
                "Team Member",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, (IReadOnlyList<string>)Array.Empty<string>()));

        _userRepository
            .Setup(x => x.GetRolesAsync(
                "user-123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string>)new[] { "Team Member" });

        _jwtTokenService
            .Setup(x => x.GenerateAccessToken(
                "user-123",
                "test@example.com",
                It.IsAny<IEnumerable<string>>()))
            .Returns(new AccessTokenResponse
            {
                AccessToken = "test-access-token",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
            });

        // Act
        var result = await _service.RegisterAsync(request);

        // Assert
        Assert.Equal("user-123", result.UserId);
        Assert.Equal("Test User", result.DisplayName);
        Assert.Equal("test@example.com", result.Email);
        Assert.Contains("Team Member", result.Roles);
        Assert.Equal("test-access-token", result.AccessToken);

        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        Assert.Equal(64, Convert.FromBase64String(result.RefreshToken).Length);

        var expectedHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(result.RefreshToken)));

        _refreshTokenRepository.Verify(
            x => x.Add(It.Is<RefreshToken>(token =>
                token.UserId == "user-123" &&
                token.TokenHash == expectedHash &&
                token.TokenHash != result.RefreshToken &&
                token.CreatedByIp == null &&
                token.ExpiresAtUtc > DateTime.UtcNow.AddDays(6) &&
                token.ExpiresAtUtc <= DateTime.UtcNow.AddDays(7))),
            Times.Once);

        Assert.InRange(
            result.RefreshTokenExpiresAtUtc,
            DateTime.UtcNow.AddDays(6),
            DateTime.UtcNow.AddDays(7));

        _unitOfWork.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _userRepository.Verify(x => x.CreateUserAsync(
            "Test User",
            "test@example.com",
            request.Password,
            It.IsAny<CancellationToken>()), Times.Once);

        _userRepository.Verify(x => x.AddToRoleAsync(
            "user-123",
            "Team Member",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_InvalidRequest_ThrowsValidationException()
    {
        var request = new RegisterRequest
        {
            DisplayName = "",
            Email = "invalid-email",
            Password = "weak"
        };

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.RegisterAsync(request));

        _userRepository.Verify(
            x => x.CreateUserAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        AssertNoRefreshTokenWasPersisted();
    }

    [Fact]
    public async Task RegisterAsync_UserCreationFails_ThrowsValidationException()
    {
        _userRepository
            .Setup(x => x.CreateUserAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                false,
                string.Empty,
                (IReadOnlyList<string>)new[] { "Email is already taken." }));

        var request = new RegisterRequest
        {
            DisplayName = "Test User",
            Email = "test@example.com",
            Password = "StrongPassword123!"
        };

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _service.RegisterAsync(request));

        Assert.Contains("Email is already taken.", exception.Message);

        _userRepository.Verify(
            x => x.AddToRoleAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        AssertNoRefreshTokenWasPersisted();
    }

    [Fact]
    public async Task RegisterAsync_RoleAssignmentFails_ThrowsInvalidOperationException()
    {
        _userRepository
            .Setup(x => x.CreateUserAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                true,
                "user-123",
                (IReadOnlyList<string>)Array.Empty<string>()));

        _userRepository
            .Setup(x => x.AddToRoleAsync(
                "user-123",
                "Team Member",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                false,
                (IReadOnlyList<string>)new[] { "Role assignment failed." }));

        var request = new RegisterRequest
        {
            DisplayName = "Test User",
            Email = "test@example.com",
            Password = "StrongPassword123!"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RegisterAsync(request));

        _jwtTokenService.Verify(
            x => x.GenerateAccessToken(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>()),
            Times.Never);
        AssertNoRefreshTokenWasPersisted();
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        var request = new LoginRequest
        {
            Email = " TEST@EXAMPLE.COM ",
            Password = "StrongPassword123!"
        };

        _userRepository
            .Setup(x => x.FindByEmailAsync(
                "test@example.com",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAccount
            {
                Id = "user-123",
                DisplayName = "Test User",
                Email = "test@example.com"
            });

        _userRepository
            .Setup(x => x.CheckPasswordAsync(
                "user-123",
                request.Password,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _userRepository
            .Setup(x => x.GetRolesAsync(
                "user-123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string>)new[] { "Team Member" });

        _jwtTokenService
            .Setup(x => x.GenerateAccessToken(
                "user-123",
                "test@example.com",
                It.IsAny<IEnumerable<string>>()))
            .Returns(new AccessTokenResponse
            {
                AccessToken = "login-access-token",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
            });

        var result = await _service.LoginAsync(request);

        Assert.Equal("user-123", result.UserId);
        Assert.Equal("test@example.com", result.Email);
        Assert.Equal("login-access-token", result.AccessToken);

        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        Assert.Equal(64, Convert.FromBase64String(result.RefreshToken).Length);

        var expectedHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(result.RefreshToken)));

        _refreshTokenRepository.Verify(
            x => x.Add(It.Is<RefreshToken>(token =>
                token.UserId == "user-123" &&
                token.TokenHash == expectedHash &&
                token.TokenHash != result.RefreshToken &&
                token.ExpiresAtUtc > DateTime.UtcNow.AddDays(6) &&
                token.ExpiresAtUtc <= DateTime.UtcNow.AddDays(7))),
            Times.Once);

        _unitOfWork.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _jwtTokenService.Verify(
            x => x.GenerateAccessToken(
                "user-123",
                "test@example.com",
                It.IsAny<IEnumerable<string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ThrowsUnauthorizedAccessException()
    {
        _userRepository
            .Setup(x => x.FindByEmailAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAccount?)null);

        var request = new LoginRequest
        {
            Email = "unknown@example.com",
            Password = "StrongPassword123!"
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.LoginAsync(request));

        _jwtTokenService.Verify(
            x => x.GenerateAccessToken(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>()),
            Times.Never);
        AssertNoRefreshTokenWasPersisted();
    }

    [Fact]
    public async Task LoginAsync_InactiveUser_ThrowsUnauthorizedAccessException()
    {
        _userRepository
            .Setup(x => x.FindByEmailAsync(
                "inactive@example.com",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAccount
            {
                Id = "inactive-user",
                DisplayName = "Inactive User",
                Email = "inactive@example.com",
                IsActive = false
            });

        var request = new LoginRequest
        {
            Email = "inactive@example.com",
            Password = "StrongPassword123!"
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.LoginAsync(request));

        _userRepository.Verify(
            x => x.CheckPasswordAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _jwtTokenService.Verify(
            x => x.GenerateAccessToken(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>()),
            Times.Never);
        AssertNoRefreshTokenWasPersisted();
    }

    [Fact]
    public async Task LoginAsync_IncorrectPassword_ThrowsUnauthorizedAccessException()
    {
        _userRepository
            .Setup(x => x.FindByEmailAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAccount
            {
                Id = "user-123",
                DisplayName = "Test User",
                Email = "test@example.com"
            });

        _userRepository
            .Setup(x => x.CheckPasswordAsync(
                "user-123",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "WrongPassword123!"
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.LoginAsync(request));

        _jwtTokenService.Verify(
            x => x.GenerateAccessToken(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>()),
            Times.Never);
        AssertNoRefreshTokenWasPersisted();
    }

    [Fact]
    public async Task LoginAsync_InvalidRequest_ThrowsValidationException()
    {
        var request = new LoginRequest
        {
            Email = "invalid-email",
            Password = ""
        };

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.LoginAsync(request));

        _userRepository.Verify(
            x => x.FindByEmailAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        AssertNoRefreshTokenWasPersisted();
    }


    [Fact]
    public async Task RefreshAsync_ValidToken_RotatesTokenAndReturnsNewCredentials()
    {
        const string rawToken = "valid-refresh-token";
        const string userId = "user-123";

        var existingToken = new RefreshToken
        {
            UserId = userId,
            TokenHash = ComputeTokenHash(rawToken),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(3)
        };

        _refreshTokenRepository
            .Setup(x => x.GetByTokenHashAsync(
                ComputeTokenHash(rawToken),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToken);

        _userRepository
            .Setup(x => x.FindByIdAsync(
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAccount
            {
                Id = userId,
                DisplayName = "Test User",
                Email = "test@example.com",
                IsActive = true
            });

        _userRepository
            .Setup(x => x.GetRolesAsync(
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string>)new[] { "Team Member" });

        _jwtTokenService
            .Setup(x => x.GenerateAccessToken(
                userId,
                "test@example.com",
                It.IsAny<IEnumerable<string>>()))
            .Returns(new AccessTokenResponse
            {
                AccessToken = "new-access-token",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
            });

        RefreshToken? replacementToken = null;

        _refreshTokenRepository
            .Setup(x => x.Add(It.IsAny<RefreshToken>()))
            .Callback<RefreshToken>(token => replacementToken = token);

        var response = await _service.RefreshAsync(
            new RefreshTokenRequest { RefreshToken = rawToken },
            ipAddress: "127.0.0.1");

        Assert.Equal(userId, response.UserId);
        Assert.Equal("Test User", response.DisplayName);
        Assert.Equal("test@example.com", response.Email);
        Assert.Equal("new-access-token", response.AccessToken);
        Assert.Equal("Team Member", Assert.Single(response.Roles));
        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
        Assert.NotEqual(rawToken, response.RefreshToken);

        Assert.NotNull(existingToken.RevokedAtUtc);
        Assert.Equal("127.0.0.1", existingToken.RevokedByIp);
        Assert.NotNull(existingToken.ReplacedByTokenId);

        Assert.NotNull(replacementToken);
        Assert.Equal(userId, replacementToken.UserId);
        Assert.Equal(existingToken.ReplacedByTokenId, replacementToken.Id);
        Assert.Equal(
            ComputeTokenHash(response.RefreshToken),
            replacementToken.TokenHash);
        Assert.NotEqual(response.RefreshToken, replacementToken.TokenHash);
        Assert.Equal("127.0.0.1", replacementToken.CreatedByIp);
        Assert.True(replacementToken.ExpiresAtUtc > DateTime.UtcNow.AddDays(6));

        _refreshTokenRepository.Verify(
            x => x.Add(It.IsAny<RefreshToken>()),
            Times.Once);

        _unitOfWork.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_UnknownToken_ThrowsUnauthorizedAccessException()
    {
        _refreshTokenRepository
            .Setup(x => x.GetByTokenHashAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.RefreshAsync(
                new RefreshTokenRequest { RefreshToken = "unknown-token" }));

        AssertNoRefreshTokenWasPersisted();
    }

    [Fact]
    public async Task RefreshAsync_ExpiredToken_ThrowsUnauthorizedAccessException()
    {
        const string rawToken = "expired-refresh-token";

        _refreshTokenRepository
            .Setup(x => x.GetByTokenHashAsync(
                ComputeTokenHash(rawToken),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshToken
            {
                UserId = "user-123",
                TokenHash = ComputeTokenHash(rawToken),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1)
            });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.RefreshAsync(
                new RefreshTokenRequest { RefreshToken = rawToken }));

        _userRepository.Verify(
            x => x.FindByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        AssertNoRefreshTokenWasPersisted();
    }

    [Fact]
    public async Task RefreshAsync_RevokedTokenWithoutReplacement_RejectsWithoutFamilyRevocation()
    {
        const string rawToken = "logged-out-refresh-token";

        _refreshTokenRepository
            .Setup(x => x.GetByTokenHashAsync(
                ComputeTokenHash(rawToken),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshToken
            {
                UserId = "user-123",
                TokenHash = ComputeTokenHash(rawToken),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(3),
                RevokedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                ReplacedByTokenId = null
            });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.RefreshAsync(
                new RefreshTokenRequest { RefreshToken = rawToken }));

        _refreshTokenRepository.Verify(
            x => x.GetActiveByUserIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        AssertNoRefreshTokenWasPersisted();
    }

    [Fact]
    public async Task RefreshAsync_ReusedRotatedToken_RevokesAllActiveTokens()
    {
        const string rawToken = "previously-rotated-token";

        var activeTokenOne = new RefreshToken
        {
            UserId = "user-123",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(5)
        };

        var activeTokenTwo = new RefreshToken
        {
            UserId = "user-123",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(6)
        };

        _refreshTokenRepository
            .Setup(x => x.GetByTokenHashAsync(
                ComputeTokenHash(rawToken),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshToken
            {
                UserId = "user-123",
                TokenHash = ComputeTokenHash(rawToken),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(2),
                RevokedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                ReplacedByTokenId = Guid.NewGuid()
            });

        _refreshTokenRepository
            .Setup(x => x.GetActiveByUserIdAsync(
                "user-123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RefreshToken>)new[]
            {
                activeTokenOne,
                activeTokenTwo
            });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.RefreshAsync(
                new RefreshTokenRequest { RefreshToken = rawToken },
                ipAddress: "192.0.2.10"));

        Assert.NotNull(activeTokenOne.RevokedAtUtc);
        Assert.NotNull(activeTokenTwo.RevokedAtUtc);
        Assert.Equal("192.0.2.10", activeTokenOne.RevokedByIp);
        Assert.Equal("192.0.2.10", activeTokenTwo.RevokedByIp);

        _refreshTokenRepository.Verify(
            x => x.Add(It.IsAny<RefreshToken>()),
            Times.Never);

        _unitOfWork.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_InactiveUser_RevokesPresentedTokenAndRejects()
    {
        const string rawToken = "inactive-user-refresh-token";

        var existingToken = new RefreshToken
        {
            UserId = "inactive-user",
            TokenHash = ComputeTokenHash(rawToken),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(3)
        };

        _refreshTokenRepository
            .Setup(x => x.GetByTokenHashAsync(
                ComputeTokenHash(rawToken),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToken);

        _userRepository
            .Setup(x => x.FindByIdAsync(
                "inactive-user",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAccount
            {
                Id = "inactive-user",
                DisplayName = "Inactive User",
                Email = "inactive@example.com",
                IsActive = false
            });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.RefreshAsync(
                new RefreshTokenRequest { RefreshToken = rawToken },
                ipAddress: "127.0.0.1"));

        Assert.NotNull(existingToken.RevokedAtUtc);
        Assert.Equal("127.0.0.1", existingToken.RevokedByIp);

        _refreshTokenRepository.Verify(
            x => x.Add(It.IsAny<RefreshToken>()),
            Times.Never);

        _unitOfWork.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_ActiveToken_RevokesToken()
    {
        const string rawToken = "logout-refresh-token";

        var existingToken = new RefreshToken
        {
            UserId = "user-123",
            TokenHash = ComputeTokenHash(rawToken),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(3)
        };

        _refreshTokenRepository
            .Setup(x => x.GetByTokenHashAsync(
                ComputeTokenHash(rawToken),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToken);

        await _service.LogoutAsync(
            new RefreshTokenRequest { RefreshToken = rawToken },
            ipAddress: "127.0.0.1");

        Assert.NotNull(existingToken.RevokedAtUtc);
        Assert.Equal("127.0.0.1", existingToken.RevokedByIp);

        _unitOfWork.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_UnknownToken_DoesNotPersistChanges()
    {
        _refreshTokenRepository
            .Setup(x => x.GetByTokenHashAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);

        await _service.LogoutAsync(
            new RefreshTokenRequest { RefreshToken = "unknown-logout-token" });

        AssertNoRefreshTokenWasPersisted();
    }

    [Fact]
    public async Task RefreshAsync_InvalidRequest_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.RefreshAsync(
                new RefreshTokenRequest { RefreshToken = string.Empty }));

        _refreshTokenRepository.Verify(
            x => x.GetByTokenHashAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        AssertNoRefreshTokenWasPersisted();
    }

    private static string ComputeTokenHash(string rawToken)
    {
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }

    private void AssertNoRefreshTokenWasPersisted()
    {
        _refreshTokenRepository.Verify(
            x => x.Add(It.IsAny<RefreshToken>()),
            Times.Never);

        _unitOfWork.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

}

using FluentValidation;
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
    private readonly Mock<IJwtTokenService> _jwtTokenService = new();

    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _service = new AuthService(
            _userRepository.Object,
            _jwtTokenService.Object,
            new RegisterRequestValidator(),
            new LoginRequestValidator());
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
    }
}

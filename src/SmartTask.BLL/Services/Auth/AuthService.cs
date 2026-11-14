using FluentValidation;
using SmartTask.BLL.DTOs.Auth;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.BLL.Interfaces.Services;

namespace SmartTask.BLL.Services.Auth;

public sealed class AuthService : IAuthService
{
    private const string DefaultRole = "Team Member";

    private readonly IUserAccountRepository _userRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;

    public AuthService(
        IUserAccountRepository userRepository,
        IJwtTokenService jwtTokenService,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator)
    {
        _userRepository = userRepository;
        _jwtTokenService = jwtTokenService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
    }

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
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

        var token = _jwtTokenService.GenerateAccessToken(
            createResult.UserId,
            email,
            roles);

        return new AuthResponse
        {
            UserId = createResult.UserId,
            DisplayName = displayName,
            Email = email,
            Roles = roles,
            AccessToken = token.AccessToken,
            ExpiresAtUtc = token.ExpiresAtUtc
        };
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
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

        if (user is null)
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

        var token = _jwtTokenService.GenerateAccessToken(
            user.Id,
            user.Email,
            roles);

        return new AuthResponse
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Roles = roles,
            AccessToken = token.AccessToken,
            ExpiresAtUtc = token.ExpiresAtUtc
        };
    }
}

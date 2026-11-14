using Microsoft.AspNetCore.Identity;
using SmartTask.BLL.Interfaces.Repositories;

namespace SmartTask.DAL.Identity;

public sealed class UserAccountRepository : IUserAccountRepository
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    public UserAccountRepository(
     UserManager<ApplicationUser> userManager,
     SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public async Task<(bool Succeeded, string UserId, IReadOnlyList<string> Errors)>
        CreateUserAsync(
            string displayName,
            string email,
            string password,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName
        };

        var result = await _userManager.CreateAsync(user, password);

        return (
            result.Succeeded,
            result.Succeeded ? user.Id : string.Empty,
            result.Errors.Select(error => error.Description).ToArray()
        );
    }

    public async Task<UserAccount?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByEmailAsync(email);

        if (user is null)
        {
            return null;
        }

        return new UserAccount
        {
            Id = user.Id,
            DisplayName = user.DisplayName ?? string.Empty,
            Email = user.Email ?? string.Empty
        };
    }

    public async Task<bool> CheckPasswordAsync(
     string userId,
     string password,
     CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return false;
        }

        var result = await _signInManager.CheckPasswordSignInAsync(
            user,
            password,
            lockoutOnFailure: true);

        return result.Succeeded;
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return [];
        }

        var roles = await _userManager.GetRolesAsync(user);

        return roles.ToArray();
    }

    public async Task<(bool Succeeded, IReadOnlyList<string> Errors)> AddToRoleAsync(
        string userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return (false, ["User was not found."]);
        }

        var result = await _userManager.AddToRoleAsync(user, role);

        return (
            result.Succeeded,
            result.Errors.Select(error => error.Description).ToArray()
        );
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartTask.BLL.DTOs.Admin;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.DAL.Persistence;
using SmartTask.Common.Constants;

namespace SmartTask.DAL.Identity;

public sealed class AdminUserRepository : IAdminUserRepository
{
    private static readonly string[] GlobalRoles =
    [
        RoleNames.Admin,
        RoleNames.ProjectManager,
        RoleNames.TeamMember
    ];

    private readonly SmartTaskDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminUserRepository(
        SmartTaskDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<(IReadOnlyList<AdminUserAccount> Items, int TotalCount)>
        GetPagedAsync(
            AdminUserListQueryRequest query,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        var usersQuery = _context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            usersQuery = usersQuery.Where(user =>
                (user.DisplayName != null && user.DisplayName.Contains(search)) ||
                (user.Email != null && user.Email.Contains(search)) ||
                (user.UserName != null && user.UserName.Contains(search)));
        }

        if (query.IsActive.HasValue)
        {
            usersQuery = usersQuery.Where(user =>
                user.IsActive == query.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var roleUserIds =
                from userRole in _context.UserRoles
                join role in _context.Roles
                    on userRole.RoleId equals role.Id
                where role.Name == query.Role
                select userRole.UserId;

            usersQuery = usersQuery.Where(user =>
                roleUserIds.Contains(user.Id));
        }

        var totalCount = await usersQuery.CountAsync(cancellationToken);

        var orderedUsersQuery = query.SortBy.ToLowerInvariant() switch
        {
            "email" => query.SortDescending
                ? usersQuery.OrderByDescending(user => user.Email)
                : usersQuery.OrderBy(user => user.Email),

            "displayname" => query.SortDescending
                ? usersQuery.OrderByDescending(user => user.DisplayName)
                : usersQuery.OrderBy(user => user.DisplayName),

            "isactive" => query.SortDescending
                ? usersQuery.OrderByDescending(user => user.IsActive)
                : usersQuery.OrderBy(user => user.IsActive),

            _ => usersQuery.OrderBy(user => user.Email)
        };

        var users = await orderedUsersQuery
            .ThenBy(user => user.Id)
            .Skip((int)((long)(query.PageNumber - 1) * query.PageSize))
            .Take(query.PageSize)
            .Select(user => new AdminUserAccount
            {
                Id = user.Id,
                DisplayName = user.DisplayName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                IsActive = user.IsActive
            })
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
        {
            return (users, totalCount);
        }

        var userIds = users.Select(user => user.Id).ToArray();

        var roleAssignments = await (
            from userRole in _context.UserRoles.AsNoTracking()
            join role in _context.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id
            where userIds.Contains(userRole.UserId)
            select new
            {
                userRole.UserId,
                RoleName = role.Name
            })
            .ToListAsync(cancellationToken);

        var rolesByUserId = roleAssignments
            .GroupBy(item => item.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Where(item => item.RoleName != null)
                    .Select(item => item.RoleName!)
                    .OrderBy(role => role)
                    .ToArray());

        foreach (var user in users)
        {
            user.Roles = rolesByUserId.TryGetValue(user.Id, out var roles)
                ? roles
                : Array.Empty<string>();
        }

        return (users, totalCount);
    }

    public async Task<AdminUserAccount?> FindByIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);

        return new AdminUserAccount
        {
            Id = user.Id,
            DisplayName = user.DisplayName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            IsActive = user.IsActive,
            Roles = roles.ToArray()
        };
    }

    public async Task<int> CountActiveInRoleAsync(
        string role,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await (
            from user in _context.Users
            join userRole in _context.UserRoles
                on user.Id equals userRole.UserId
            join identityRole in _context.Roles
                on userRole.RoleId equals identityRole.Id
            where user.IsActive && identityRole.Name == role
            select user.Id)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    public async Task<(bool Succeeded, IReadOnlyList<string> Errors)>
        SetActiveAsync(
            string userId,
            bool isActive,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return (false, ["User was not found."]);
        }

        user.IsActive = isActive;

        var result = await _userManager.UpdateAsync(user);

        return (
            result.Succeeded,
            result.Errors.Select(error => error.Description).ToArray()
        );
    }

    public async Task<(bool Succeeded, IReadOnlyList<string> Errors)>
        ReplaceRoleAsync(
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

        if (!GlobalRoles.Contains(role, StringComparer.Ordinal))
        {
            return (false, ["The requested application role is invalid."]);
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles
            .Where(currentRole =>
                GlobalRoles.Contains(currentRole, StringComparer.Ordinal))
            .ToArray();

        if (rolesToRemove.Length > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(
                user,
                rolesToRemove);

            if (!removeResult.Succeeded)
            {
                return (
                    false,
                    removeResult.Errors
                        .Select(error => error.Description)
                        .ToArray());
            }
        }

        var addResult = await _userManager.AddToRoleAsync(user, role);

        return (
            addResult.Succeeded,
            addResult.Errors.Select(error => error.Description).ToArray()
        );
    }
}

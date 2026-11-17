using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartTask.DAL.Identity;
using SmartTask.Common.Constants;

namespace SmartTask.API.Development;

public static class DevelopmentAdminSeeder
{
    public static async Task SeedAsync(
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        var email = configuration["DevelopmentAdmin:Email"];
        var password = configuration["DevelopmentAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Development Admin credentials are missing from User Secrets.");
        }

        using var scope = serviceProvider.CreateScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Development Administrator",
                IsActive = true
            };

            var createResult = await userManager.CreateAsync(user, password);

            if (!createResult.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    createResult.Errors.Select(error => error.Description));

                throw new InvalidOperationException(
                    $"Development Admin creation failed: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, RoleNames.Admin))
        {
            if (await userManager.GetRolesAsync(user) is { Count: > 0 })
            {
                throw new InvalidOperationException(
                    "Development Admin email belongs to an account with another role. Use a dedicated email.");
            }

            var roleResult = await userManager.AddToRoleAsync(user, RoleNames.Admin);

            if (!roleResult.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    roleResult.Errors.Select(error => error.Description));

                throw new InvalidOperationException(
                    $"Development Admin role assignment failed: {errors}");
            }
        }
    }
}

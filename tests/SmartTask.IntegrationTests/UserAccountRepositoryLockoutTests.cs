using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.DAL.Identity;
using SmartTask.DAL.Persistence;

namespace SmartTask.IntegrationTests;

public sealed class UserAccountRepositoryLockoutTests
{
    [Fact]
    public async Task FindByIdAsync_ExistingUser_ReturnsUserAccount()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddAuthentication();

        services.AddDbContext<SmartTaskDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<SmartTaskDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddScoped<IUserAccountRepository, UserAccountRepository>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<IUserAccountRepository>();

        var creation = await repository.CreateUserAsync(
            "Lookup Test",
            $"lookup-{Guid.NewGuid():N}@example.com",
            "LookupTest@2026Secure");

        Assert.True(creation.Succeeded);

        var account = await repository.FindByIdAsync(creation.UserId);

        Assert.NotNull(account);
        Assert.Equal(creation.UserId, account.Id);
        Assert.Equal("Lookup Test", account.DisplayName);
    }

    [Fact]
    public async Task FindByIdAsync_UnknownUser_ReturnsNull()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddAuthentication();

        services.AddDbContext<SmartTaskDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<SmartTaskDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddScoped<IUserAccountRepository, UserAccountRepository>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<IUserAccountRepository>();

        var account = await repository.FindByIdAsync("missing-user-id");

        Assert.Null(account);
    }

    [Fact]
    public async Task CheckPasswordAsync_FiveFailedAttempts_LocksAccount()
    {
        // Arrange: create an isolated Identity environment.
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddAuthentication();

        services.AddDbContext<SmartTaskDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;

            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan =
                TimeSpan.FromMinutes(15);
            options.Lockout.AllowedForNewUsers = true;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<SmartTaskDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

        services.AddScoped<IUserAccountRepository, UserAccountRepository>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<IUserAccountRepository>();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        const string password = "LockoutTest@2026Secure";
        const string wrongPassword = "WrongPassword@2026";

        var email = $"lockout-{Guid.NewGuid():N}@example.com";

        var creation = await repository.CreateUserAsync(
            "Lockout Test",
            email,
            password);

        Assert.True(
            creation.Succeeded,
            string.Join("; ", creation.Errors));

        // Act: submit four incorrect passwords.
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var result = await repository.CheckPasswordAsync(
                creation.UserId,
                wrongPassword);

            Assert.False(result);

            var user = await userManager.FindByIdAsync(creation.UserId);

            Assert.NotNull(user);
            Assert.Equal(attempt, user.AccessFailedCount);
            Assert.Null(user.LockoutEnd);
        }

        // Act: the fifth incorrect password should trigger lockout.
        var fifthAttempt = await repository.CheckPasswordAsync(
            creation.UserId,
            wrongPassword);

        // Assert: password is rejected and account is locked.
        Assert.False(fifthAttempt);

        var lockedUser = await userManager.FindByIdAsync(creation.UserId);

        Assert.NotNull(lockedUser);
        Assert.True(lockedUser.LockoutEnabled);
        Assert.Equal(0, lockedUser.AccessFailedCount);
        Assert.NotNull(lockedUser.LockoutEnd);
        Assert.True(lockedUser.LockoutEnd > DateTimeOffset.UtcNow);

        // Even the correct password must be rejected during lockout.
        var correctPasswordResult = await repository.CheckPasswordAsync(
            creation.UserId,
            password);

        Assert.False(correctPasswordResult);
    }
}

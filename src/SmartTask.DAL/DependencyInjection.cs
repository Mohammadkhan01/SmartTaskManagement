
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartTask.BLL.Interfaces.Repositories;
using SmartTask.DAL.Identity;
using SmartTask.DAL.Persistence;
using SmartTask.DAL.Persistence.Repositories;
using SmartTask.BLL.Interfaces.Services;
using SmartTask.DAL.Storage;
using SmartTask.BLL.Configuration;
using SmartTask.DAL.Ai;

namespace SmartTask.DAL;

public static class DependencyInjection
{
    public static IServiceCollection AddDal(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string 'DefaultConnection' is missing.");
        }

        // Database context
        services.AddDbContext<SmartTaskDbContext>(options =>
            options.UseSqlServer(connectionString));

        // ASP.NET Core Identity
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;

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

        // Repository registrations
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectMemberRepository, ProjectMemberRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<ITaskCommentRepository, TaskCommentRepository>();
        services.AddScoped<ITaskAttachmentRepository, TaskAttachmentRepository>();
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IAdminUserRepository, AdminUserRepository>();
        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();

        // AI providers (strategy selected by Ai:Provider in AIService)
        services.Configure<AiSettings>(
            configuration.GetSection(AiSettings.SectionName));
        services.AddSingleton<IAiTextProvider, MockAiTextProvider>();
        services.AddHttpClient<OpenAiCompatibleTextProvider>();
        services.AddTransient<IAiTextProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<OpenAiCompatibleTextProvider>());
        services.AddHttpClient<GitHubModelsTextProvider>();
        services.AddTransient<IAiTextProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<GitHubModelsTextProvider>());

        return services;
    }
}

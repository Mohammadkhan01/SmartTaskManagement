using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SmartTask.BLL.DTOs.Auth;
using SmartTask.BLL.DTOs.Projects;
using SmartTask.BLL.Interfaces.Services;
using SmartTask.BLL.Services.Auth;
using SmartTask.BLL.Services.Projects;
using SmartTask.BLL.Validators;
using SmartTask.BLL.Validators.Auth;

namespace SmartTask.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddBll(
        this IServiceCollection services)
    {
        // Project services
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<
            IValidator<CreateProjectRequest>,
            CreateProjectRequestValidator>();
        services.AddScoped<
    IValidator<UpdateProjectRequest>,
    UpdateProjectRequestValidator>();

        // Authentication services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<
            IValidator<RegisterRequest>,
            RegisterRequestValidator>();
        services.AddScoped<
            IValidator<LoginRequest>,
            LoginRequestValidator>();

        return services;
    }
}

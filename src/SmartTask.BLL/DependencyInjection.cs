using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SmartTask.BLL.DTOs.Auth;
using SmartTask.BLL.DTOs.Projects;
using SmartTask.BLL.Interfaces.Services;
using SmartTask.BLL.Services.Auth;
using SmartTask.BLL.Services.Projects;
using SmartTask.BLL.Validators;
using SmartTask.BLL.Validators.Auth;
using SmartTask.BLL.DTOs.Tasks;
using SmartTask.BLL.Services.Tasks;
using SmartTask.BLL.DTOs.TaskComments;
using SmartTask.BLL.Services.TaskComments;
using SmartTask.BLL.Services.TaskAttachments;
using SmartTask.BLL.Services.Notifications;

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
        // Task services
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<
            IValidator<RegisterRequest>,
            RegisterRequestValidator>();
        services.AddScoped<
            IValidator<LoginRequest>,
            LoginRequestValidator>();
        // Task validators
        services.AddScoped<
            IValidator<CreateTaskRequest>,
            CreateTaskRequestValidator>();

        services.AddScoped<
            IValidator<UpdateTaskRequest>,
            UpdateTaskRequestValidator>();

        services.AddScoped<
            IValidator<UpdateTaskStatusRequest>,
            UpdateTaskStatusRequestValidator>();

        services.AddScoped<
            IValidator<CreateTaskCommentRequest>,
            CreateTaskCommentRequestValidator>();

        services.AddScoped<
            IValidator<UpdateTaskCommentRequest>,
            UpdateTaskCommentRequestValidator>();

        services.AddScoped<
            ITaskCommentService, TaskCommentService>();

        services.AddScoped<
            ITaskActivityService, TaskActivityService>();

        services.AddScoped<
            INotificationService, NotificationService>();

        services.AddScoped<ITaskAttachmentService, TaskAttachmentService>();

        return services;
    }
}

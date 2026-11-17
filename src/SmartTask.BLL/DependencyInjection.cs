using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SmartTask.BLL.DTOs.Auth;
using SmartTask.BLL.DTOs.Admin;
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
using SmartTask.BLL.Services.Dashboard;
using SmartTask.BLL.Services.Admin;
using SmartTask.BLL.Validators.Admin;
using SmartTask.BLL.DTOs.AI;
using SmartTask.BLL.Services.AI;

namespace SmartTask.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddBll(
        this IServiceCollection services)
    {
        // Admin services
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IValidator<AdminUserListQueryRequest>, AdminUserListQueryRequestValidator>();
        services.AddScoped<IValidator<UpdateUserRoleRequest>, UpdateUserRoleRequestValidator>();
        services.AddScoped<IValidator<UpdateUserStatusRequest>, UpdateUserStatusRequestValidator>();

        // Project services
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<
            IValidator<CreateProjectRequest>,
            CreateProjectRequestValidator>();
        services.AddScoped<
    IValidator<UpdateProjectRequest>,
    UpdateProjectRequestValidator>();
        services.AddScoped<
            IValidator<ProjectListQueryRequest>,
            ProjectListQueryRequestValidator>();

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
        services.AddScoped<
            IValidator<RefreshTokenRequest>,
            RefreshTokenRequestValidator>();
        // Task validators
        services.AddScoped<
            IValidator<CreateTaskRequest>,
            CreateTaskRequestValidator>();

        services.AddScoped<
            IValidator<UpdateTaskRequest>,
            UpdateTaskRequestValidator>();

        services.AddScoped<
            IValidator<AssignTaskRequest>,
            AssignTaskRequestValidator>();

        services.AddScoped<
            IValidator<UpdateTaskStatusRequest>,
            UpdateTaskStatusRequestValidator>();

        services.AddScoped<
            IValidator<TaskListQueryRequest>,
            TaskListQueryRequestValidator>();

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

        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ITaskAttachmentService, TaskAttachmentService>();

        // AI services
        services.AddScoped<IAIService, AIService>();
        services.AddScoped<
            IValidator<EnhanceTaskDescriptionRequest>,
            EnhanceTaskDescriptionRequestValidator>();

        return services;
    }
}

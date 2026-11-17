using FluentValidation;
using SmartTask.BLL.DTOs.Admin;
using SmartTask.Common.Constants;

namespace SmartTask.BLL.Validators.Admin;

public sealed class UpdateUserRoleRequestValidator
    : AbstractValidator<UpdateUserRoleRequest>
{
    private static readonly string[] AllowedRoles =
    [
        RoleNames.Admin,
        RoleNames.ProjectManager,
        RoleNames.TeamMember
    ];

    public UpdateUserRoleRequestValidator()
    {
        RuleFor(x => x.Role)
            .NotEmpty()
            .WithMessage("Role is required.")
            .Must(role =>
                !string.IsNullOrWhiteSpace(role) &&
                AllowedRoles.Contains(
                    role.Trim(),
                    StringComparer.OrdinalIgnoreCase))
            .WithMessage("Role must be Admin, Project Manager, or Team Member.");
    }
}

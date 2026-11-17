using FluentValidation;
using SmartTask.BLL.DTOs.Admin;

namespace SmartTask.BLL.Validators.Admin;

public sealed class UpdateUserRoleRequestValidator
    : AbstractValidator<UpdateUserRoleRequest>
{
    private static readonly string[] AllowedRoles =
    [
        "Admin",
        "Project Manager",
        "Team Member"
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

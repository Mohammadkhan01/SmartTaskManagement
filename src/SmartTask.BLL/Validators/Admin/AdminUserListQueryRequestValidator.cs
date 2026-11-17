using FluentValidation;
using SmartTask.BLL.DTOs.Admin;

namespace SmartTask.BLL.Validators.Admin;

public sealed class AdminUserListQueryRequestValidator
    : AbstractValidator<AdminUserListQueryRequest>
{
    private static readonly string[] AllowedRoles =
    [
        "Admin",
        "Project Manager",
        "Team Member"
    ];

    private static readonly string[] AllowedSortFields =
    [
        "email",
        "displayName",
        "isActive"
    ];

    public AdminUserListQueryRequestValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x)
            .Must(x =>
                x.PageNumber >= 1 &&
                x.PageSize >= 1 &&
                (long)(x.PageNumber - 1) * x.PageSize <= int.MaxValue)
            .WithMessage("Requested page is outside the supported range.");

        RuleFor(x => x.Search)
            .MaximumLength(200)
            .WithMessage("Search cannot exceed 200 characters.");

        RuleFor(x => x.Role)
            .Must(role =>
                string.IsNullOrWhiteSpace(role) ||
                AllowedRoles.Contains(
                    role.Trim(),
                    StringComparer.OrdinalIgnoreCase))
            .WithMessage("Role must be Admin, Project Manager, or Team Member.");

        RuleFor(x => x.SortBy)
            .Must(sortBy =>
                !string.IsNullOrWhiteSpace(sortBy) &&
                AllowedSortFields.Contains(
                    sortBy,
                    StringComparer.OrdinalIgnoreCase))
            .WithMessage("Sort field must be email, displayName, or isActive.");
    }
}

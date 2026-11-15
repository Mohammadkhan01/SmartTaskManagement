using FluentValidation;
using SmartTask.BLL.DTOs.Projects;
using SmartTask.Domain.Enums;

namespace SmartTask.BLL.Validators;

public sealed class ProjectListQueryRequestValidator
    : AbstractValidator<ProjectListQueryRequest>
{
    private static readonly string[] AllowedSortFields =
    [
        "createdAt",
        "updatedAt",
        "name",
        "status"
    ];

    public ProjectListQueryRequestValidator()
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

        RuleFor(x => x.Status)
            .Must(status =>
                !status.HasValue ||
                Enum.IsDefined(typeof(ProjectStatus), status.Value))
            .WithMessage("Invalid project status.");

        RuleFor(x => x.SortBy)
            .Must(sortBy =>
                !string.IsNullOrWhiteSpace(sortBy) &&
                AllowedSortFields.Contains(
                    sortBy,
                    StringComparer.OrdinalIgnoreCase))
            .WithMessage(
                "Sort field must be one of: createdAt, updatedAt, name, status.");
    }
}

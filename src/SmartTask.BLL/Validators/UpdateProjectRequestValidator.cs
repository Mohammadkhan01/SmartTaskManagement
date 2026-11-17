using FluentValidation;
using SmartTask.BLL.DTOs.Projects;
using SmartTask.Domain.Enums;

namespace SmartTask.BLL.Validators;

public class UpdateProjectRequestValidator
    : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Project name is required.")
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Project name cannot contain only whitespace.")
            .MaximumLength(150)
            .WithMessage("Project name cannot exceed 150 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .WithMessage("Project description cannot exceed 2000 characters.");

        RuleFor(x => x.Status)
            .Must(status =>
                !status.HasValue ||
                Enum.IsDefined(typeof(ProjectStatus), status.Value))
            .WithMessage("Invalid project status.");

        RuleFor(x => x.RowVersion)
    .NotEmpty()
    .WithMessage("Project version is required.")
    .Must(BeValidBase64)
    .WithMessage("Project version must be a valid 8-byte Base64 value.");
    }

    private static bool BeValidBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            return Convert.FromBase64String(value).Length == 8;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

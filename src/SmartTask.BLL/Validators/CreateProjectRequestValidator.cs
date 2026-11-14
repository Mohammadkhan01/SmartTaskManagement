using FluentValidation;
using SmartTask.BLL.DTOs.Projects;

namespace SmartTask.BLL.Validators;

public class CreateProjectRequestValidator
    : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
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
    }
}

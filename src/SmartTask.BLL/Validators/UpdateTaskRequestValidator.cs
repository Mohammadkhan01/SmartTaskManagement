using FluentValidation;
using SmartTask.BLL.DTOs.Tasks;

namespace SmartTask.BLL.Validators;

public class UpdateTaskRequestValidator
    : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .Must(title => !string.IsNullOrWhiteSpace(title))
            .WithMessage("Task title is required.")
            .MaximumLength(200)
            .WithMessage("Task title cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(5000)
            .WithMessage("Task description cannot exceed 5000 characters.");

        RuleFor(x => x.Priority)
            .IsInEnum()
            .WithMessage("Invalid task priority.");

        RuleFor(x => x.AssignedToUserId)
            .MaximumLength(450)
            .WithMessage("Assigned user ID cannot exceed 450 characters.")
            .Must(id => id is null || !string.IsNullOrWhiteSpace(id))
            .WithMessage("Assigned user ID cannot contain only whitespace.");

        RuleFor(x => x.RowVersion)
            .NotEmpty()
            .Must(BeValidRowVersion)
            .WithMessage("Task version must be a valid 8-byte Base64 value.");
    }

    private static bool BeValidRowVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

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
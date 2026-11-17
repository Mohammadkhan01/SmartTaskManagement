using FluentValidation;
using SmartTask.BLL.DTOs.Tasks;

namespace SmartTask.BLL.Validators;

public class AssignTaskRequestValidator : AbstractValidator<AssignTaskRequest>
{
    public AssignTaskRequestValidator()
    {
        RuleFor(x => x.AssignedToUserId)
            .NotEmpty()
            .MaximumLength(450)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("A valid assignee user ID is required.");

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

using FluentValidation;
using SmartTask.BLL.DTOs.Tasks;

namespace SmartTask.BLL.Validators;

public class UpdateTaskStatusRequestValidator
    : AbstractValidator<UpdateTaskStatusRequest>
{
    public UpdateTaskStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage("Invalid task status.");

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
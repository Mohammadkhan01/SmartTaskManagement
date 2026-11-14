using FluentValidation;
using SmartTask.BLL.DTOs.TaskComments;

namespace SmartTask.BLL.Validators;

public class UpdateTaskCommentRequestValidator
    : AbstractValidator<UpdateTaskCommentRequest>
{
    public UpdateTaskCommentRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty()
            .Must(content => !string.IsNullOrWhiteSpace(content))
            .WithMessage("Comment content is required.")
            .MaximumLength(5000);

        RuleFor(x => x.RowVersion)
            .NotEmpty()
            .Must(IsValidRowVersion)
            .WithMessage("A valid 8-byte RowVersion is required.");
    }

    private static bool IsValidRowVersion(string value)
    {
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

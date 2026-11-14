using FluentValidation;
using SmartTask.BLL.DTOs.TaskComments;

namespace SmartTask.BLL.Validators;

public class CreateTaskCommentRequestValidator
    : AbstractValidator<CreateTaskCommentRequest>
{
    public CreateTaskCommentRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty()
            .Must(content => !string.IsNullOrWhiteSpace(content))
            .WithMessage("Comment content is required.")
            .MaximumLength(5000);
    }
}

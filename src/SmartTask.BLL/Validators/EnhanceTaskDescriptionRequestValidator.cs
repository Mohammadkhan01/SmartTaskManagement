using FluentValidation;
using SmartTask.BLL.DTOs.AI;
using SmartTask.BLL.Services.AI;
using SmartTask.Common.Constants;

namespace SmartTask.BLL.Validators;

public class EnhanceTaskDescriptionRequestValidator
    : AbstractValidator<EnhanceTaskDescriptionRequest>
{
    public const int MaxTitleLength = FieldLimits.TaskTitleMaxLength;

    public const int MaxDescriptionLength = FieldLimits.TaskDescriptionMaxLength;

    public EnhanceTaskDescriptionRequestValidator()
    {
        RuleFor(x => x.Title)
            .MaximumLength(MaxTitleLength)
            .WithMessage($"Task title cannot exceed {MaxTitleLength} characters.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .Must(description =>
                TaskDescriptionPrompt.Sanitize(description).Length > 0)
            .WithMessage("Task description is required.")
            .MaximumLength(MaxDescriptionLength)
            .WithMessage(
                $"Task description cannot exceed {MaxDescriptionLength} characters.");
    }
}

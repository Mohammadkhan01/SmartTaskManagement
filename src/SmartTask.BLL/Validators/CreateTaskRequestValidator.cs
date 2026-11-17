using FluentValidation;
using SmartTask.BLL.DTOs.Tasks;
using SmartTask.Common.Constants;

namespace SmartTask.BLL.Validators;

public class CreateTaskRequestValidator
    : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .Must(title => !string.IsNullOrWhiteSpace(title))
            .WithMessage("Task title is required.")
            .MaximumLength(FieldLimits.TaskTitleMaxLength)
            .WithMessage(
                $"Task title cannot exceed {FieldLimits.TaskTitleMaxLength} characters.");

        RuleFor(x => x.Description)
            .MaximumLength(FieldLimits.TaskDescriptionMaxLength)
            .WithMessage(
                $"Task description cannot exceed {FieldLimits.TaskDescriptionMaxLength} characters.");

        RuleFor(x => x.Priority)
            .IsInEnum()
            .WithMessage("Invalid task priority.");

        // Empty / whitespace IDs are normalized to null in TaskService before validation.
        RuleFor(x => x.AssignedToUserId)
            .MaximumLength(450)
            .When(x => x.AssignedToUserId is not null)
            .WithMessage("Assigned user ID cannot exceed 450 characters.");
    }
}
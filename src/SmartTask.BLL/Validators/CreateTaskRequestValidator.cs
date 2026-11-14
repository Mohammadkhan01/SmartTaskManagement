using FluentValidation;
using SmartTask.BLL.DTOs.Tasks;

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
    }
}
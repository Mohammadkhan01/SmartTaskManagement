using FluentValidation;
using SmartTask.BLL.DTOs.Tasks;
using SmartTask.Domain.Enums;
using TaskStatus = SmartTask.Domain.Enums.TaskStatus;

namespace SmartTask.BLL.Validators;

public sealed class TaskListQueryRequestValidator
    : AbstractValidator<TaskListQueryRequest>
{
    private static readonly string[] AllowedSortFields =
    [
        "createdAt",
        "updatedAt",
        "dueDate",
        "title",
        "priority",
        "status"
    ];

    public TaskListQueryRequestValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x)
            .Must(x =>
                x.PageNumber >= 1 &&
                x.PageSize >= 1 &&
                (long)(x.PageNumber - 1) * x.PageSize <= int.MaxValue)
            .WithMessage("Requested page is outside the supported range.");

        RuleFor(x => x.Search)
            .MaximumLength(200)
            .WithMessage("Search cannot exceed 200 characters.");

        RuleFor(x => x.Status)
            .Must(status =>
                !status.HasValue ||
                Enum.IsDefined(typeof(TaskStatus), status.Value))
            .WithMessage("Invalid task status.");

        RuleFor(x => x.Priority)
            .Must(priority =>
                !priority.HasValue ||
                Enum.IsDefined(typeof(TaskPriority), priority.Value))
            .WithMessage("Invalid task priority.");

        RuleFor(x => x.AssignedToUserId)
            .MaximumLength(450)
            .WithMessage("Assigned user ID cannot exceed 450 characters.")
            .Must(id => id is null || !string.IsNullOrWhiteSpace(id))
            .WithMessage("Assigned user ID cannot contain only whitespace.");

        RuleFor(x => x)
            .Must(x =>
                !x.DueDateFromUtc.HasValue ||
                !x.DueDateToUtc.HasValue ||
                x.DueDateFromUtc.Value <= x.DueDateToUtc.Value)
            .WithMessage("Due date start must be earlier than or equal to due date end.");

        RuleFor(x => x.SortBy)
            .Must(sortBy =>
                !string.IsNullOrWhiteSpace(sortBy) &&
                AllowedSortFields.Contains(
                    sortBy,
                    StringComparer.OrdinalIgnoreCase))
            .WithMessage(
                "Sort field must be one of: createdAt, updatedAt, dueDate, title, priority, status.");
    }
}

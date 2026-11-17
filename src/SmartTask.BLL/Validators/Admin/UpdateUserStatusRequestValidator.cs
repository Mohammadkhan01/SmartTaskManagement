using FluentValidation;
using SmartTask.BLL.DTOs.Admin;

namespace SmartTask.BLL.Validators.Admin;

public sealed class UpdateUserStatusRequestValidator
    : AbstractValidator<UpdateUserStatusRequest>
{
    public UpdateUserStatusRequestValidator()
    {
        RuleFor(x => x.IsActive)
            .NotNull()
            .WithMessage("IsActive must be explicitly provided.");
    }
}

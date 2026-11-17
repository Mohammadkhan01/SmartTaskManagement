using FluentValidation;
using SmartTask.BLL.DTOs.Auth;

namespace SmartTask.BLL.Validators.Auth;

public sealed class RefreshTokenRequestValidator
    : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .MaximumLength(256);
    }
}

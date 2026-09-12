using FluentValidation;

namespace FocusLens.Application.Features.Identity.Commands.ResendVerificationCode;

public sealed class ResendVerificationCodeCommandValidator
    : AbstractValidator<ResendVerificationCodeCommand>
{
    public ResendVerificationCodeCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress();
    }
}
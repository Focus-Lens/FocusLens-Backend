using FluentValidation;

namespace FocusLens.Application.Features.Identity.Commands.VerifyEmail;

public sealed class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(command => command.Otp)
            .NotEmpty()
            .Matches("^\\d{6}$")
            .WithMessage("OTP must be exactly 6 digits.");
    }
}
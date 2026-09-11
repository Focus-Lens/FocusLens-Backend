using FluentValidation;

namespace FocusLens.Application.Features.Identity.Commands.RegisterParent;

public sealed class RegisterParentCommandValidator
    : AbstractValidator<RegisterParentCommand>
{
    public RegisterParentCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(8);

        RuleFor(command => command.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.AcceptTerms)
            .Equal(true)
            .WithMessage("Terms and conditions must be accepted.");

        RuleFor(command => command.TermsId)
            .NotEmpty()
            .When(command => command.AcceptTerms);
    }
}

using FluentValidation;

namespace FocusLens.Application.Features.Identity.Commands.RegisterStudent;

public sealed class RegisterStudentCommandValidator
    : AbstractValidator<RegisterStudentCommand>
{
    public RegisterStudentCommandValidator()
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
    }
}

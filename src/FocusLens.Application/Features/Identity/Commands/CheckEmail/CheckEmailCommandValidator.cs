using FluentValidation;

namespace FocusLens.Application.Features.Identity.Commands.CheckEmail;

public sealed class CheckEmailCommandValidator
    : AbstractValidator<CheckEmailCommand>
{
    public CheckEmailCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress();
    }
}

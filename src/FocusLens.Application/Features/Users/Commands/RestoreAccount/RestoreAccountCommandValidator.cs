using FluentValidation;

namespace FocusLens.Application.Features.Users.Commands.RestoreAccount;

public sealed class RestoreAccountCommandValidator : AbstractValidator<RestoreAccountCommand>
{
    public RestoreAccountCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(command => command.Password)
            .NotEmpty();
    }
}

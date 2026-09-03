using FluentValidation;

namespace FocusLens.Application.Features.Identity.Commands.RefreshToken;

public sealed class RefreshTokenCommandValidator
    : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(command => command.AccessToken)
            .NotEmpty();

        RuleFor(command => command.RefreshToken)
            .NotEmpty();
    }
}

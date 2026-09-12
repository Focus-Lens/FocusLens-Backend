using FluentValidation;

namespace FocusLens.Application.Features.Identity.Commands.RevokeRefreshToken;

public sealed class RevokeRefreshTokenCommandValidator
    : AbstractValidator<RevokeRefreshTokenCommand>
{
    public RevokeRefreshTokenCommandValidator()
    {
        RuleFor(command => command.RefreshToken)
            .NotEmpty();
    }
}
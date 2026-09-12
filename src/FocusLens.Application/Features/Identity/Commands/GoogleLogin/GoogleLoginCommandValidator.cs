using FluentValidation;

namespace FocusLens.Application.Features.Identity.Commands.GoogleLogin;

public sealed class GoogleLoginCommandValidator
    : AbstractValidator<GoogleLoginCommand>
{
    public GoogleLoginCommandValidator()
    {
        RuleFor(command => command.IdToken)
            .NotEmpty();
    }
}
using FluentValidation;

namespace FocusLens.Application.Features.Users.Commands.UpdateCurrentUser;

public sealed class UpdateCurrentUserCommandValidator
    : AbstractValidator<UpdateCurrentUserCommand>
{
    public UpdateCurrentUserCommandValidator()
    {
        RuleFor(command => command.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.LastName)
            .NotEmpty()
            .MaximumLength(100);
    }
}

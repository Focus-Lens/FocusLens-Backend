using FluentValidation;

namespace FocusLens.Application.Terms;

public sealed class SubmitTermsDecisionCommandValidator
    : AbstractValidator<SubmitTermsDecisionCommand>
{
    public SubmitTermsDecisionCommandValidator()
    {
        RuleFor(command => command.Request.Accepted)
            .NotNull();

        RuleFor(command => command.Request.TermsId)
            .NotEmpty()
            .When(command => command.Request.Accepted == true);
    }
}

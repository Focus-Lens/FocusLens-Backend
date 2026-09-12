using FocusLens.Contracts.Terms;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Terms;

public sealed class SubmitTermsDecisionCommandHandler
    : IRequestHandler<SubmitTermsDecisionCommand, Result<TermsDecisionResponse>>
{
    public Task<Result<TermsDecisionResponse>> Handle(
        SubmitTermsDecisionCommand request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<Result<TermsDecisionResponse>>(
            new TermsDecisionResponse(request.Request.Accepted!.Value));
    }
}
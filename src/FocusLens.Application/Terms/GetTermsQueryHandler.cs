using FocusLens.Contracts.Terms;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Terms;

public sealed class GetTermsQueryHandler(ITermsProvider termsProvider)
    : IRequestHandler<GetTermsQuery, Result<TermsResponse>>
{
    public Task<Result<TermsResponse>> Handle(
        GetTermsQuery request,
        CancellationToken cancellationToken) =>
        Task.FromResult<Result<TermsResponse>>(termsProvider.GetCurrentTerms());
}
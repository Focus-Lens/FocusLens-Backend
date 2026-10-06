using FocusLens.Application.Common.Errors;
using FocusLens.Contracts.Terms;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Terms;

public sealed class GetTermsQueryHandler(
    IBaseRepository<LegalDocument> legalDocuments)
    : IRequestHandler<GetTermsQuery, Result<TermsResponse>>
{
    public async Task<Result<TermsResponse>> Handle(
        GetTermsQuery request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse(request.Audience, true, out LegalDocumentAudience audience)
            || !Enum.IsDefined(audience))
        {
            return ApplicationErrors.Terms.InvalidAudience;
        }

        IEnumerable<LegalDocument> publishedDocuments = await legalDocuments.GetAllAsync(document =>
            document.Audience == audience
            && document.Type == LegalDocumentType.Terms
            && document.IsPublished
            && document.PublishedOnUtc != null);

        LegalDocument? terms = publishedDocuments
            .OrderByDescending(document => document.PublishedOnUtc)
            .FirstOrDefault();

        if (terms is null || terms.PublishedOnUtc is null)
        {
            return ApplicationErrors.Terms.NotFound;
        }

        return new TermsResponse(
            terms.Id,
            terms.Audience.ToString(),
            terms.Version,
            terms.Content,
            terms.PublishedOnUtc.Value);
    }
}

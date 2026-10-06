using FocusLens.Application.Common.Errors;
using FocusLens.Contracts.Terms;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Terms;

public sealed class GetPrivacyQueryHandler(
    IBaseRepository<LegalDocument> legalDocuments) : IRequestHandler<GetPrivacyQuery, Result<TermsResponse>>
{
    public async Task<Result<TermsResponse>> Handle(GetPrivacyQuery request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse(request.Audience, true, out LegalDocumentAudience audience)
            || !Enum.IsDefined(audience))
        {
            return ApplicationErrors.Terms.InvalidAudience;
        }

        LegalDocument? privacy = (await legalDocuments.GetAllAsync(document =>
                document.Audience == audience
                && document.Type == LegalDocumentType.Privacy
                && document.IsPublished
                && document.PublishedOnUtc != null))
            .OrderByDescending(document => document.PublishedOnUtc)
            .FirstOrDefault();
        if (privacy?.PublishedOnUtc is null) return ApplicationErrors.Terms.NotFound;

        return new TermsResponse(privacy.Id, privacy.Audience.ToString(), privacy.Version,
            privacy.Content, privacy.PublishedOnUtc.Value);
    }
}

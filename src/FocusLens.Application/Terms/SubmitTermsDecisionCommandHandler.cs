using FocusLens.Contracts.Terms;
using FocusLens.Application.Common.Errors;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Terms;

public sealed class SubmitTermsDecisionCommandHandler(
    IBaseRepository<LegalDocument> legalDocuments,
    IBaseRepository<UserTermsAcceptance> acceptances,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<SubmitTermsDecisionCommand, Result<TermsDecisionResponse>>
{
    public async Task<Result<TermsDecisionResponse>> Handle(
        SubmitTermsDecisionCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Request.Accepted != true)
        {
            return new TermsDecisionResponse(
                Accepted: false,
                TermsId: request.Request.TermsId ?? Guid.Empty,
                Audience: string.Empty,
                Version: string.Empty,
                AcceptedAtUtc: null);
        }

        Guid userId = currentUser.UserId ?? Guid.Empty;

        if (userId == Guid.Empty)
        {
            return ApplicationErrors.Terms.CurrentUserUnavailable;
        }

        if (request.Request.TermsId is null)
        {
            return ApplicationErrors.Terms.DocumentNotFound;
        }

        LegalDocument? terms = await legalDocuments.GetByIdAsync(request.Request.TermsId.Value);

        if (terms is null)
        {
            return ApplicationErrors.Terms.DocumentNotFound;
        }

        if (!terms.IsPublished || terms.PublishedOnUtc is null)
        {
            return ApplicationErrors.Terms.DocumentNotPublished;
        }

        UserTermsAcceptance? existingAcceptance = await acceptances.FirstOrDefaultAsync(
            acceptance => acceptance.UserId == userId
                && acceptance.LegalDocumentId == terms.Id);

        DateTimeOffset acceptedAtUtc = existingAcceptance?.AcceptedAtUtc
            ?? timeProvider.GetUtcNow();

        if (existingAcceptance is null)
        {
            acceptances.Add(new UserTermsAcceptance(
                userId,
                terms.Id,
                acceptedAtUtc));

            await unitOfWork.SaveChangesAsync();
        }

        return new TermsDecisionResponse(
            Accepted: true,
            TermsId: terms.Id,
            Audience: terms.Audience.ToString(),
            Version: terms.Version,
            AcceptedAtUtc: acceptedAtUtc);
    }
}

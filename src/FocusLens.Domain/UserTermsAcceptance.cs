using FocusLens.Domain.Common;
using FocusLens.Domain.Identity;

namespace FocusLens.Domain;

public sealed class UserTermsAcceptance : Entity
{
    private UserTermsAcceptance()
    {
    }

    public UserTermsAcceptance(Guid userId, Guid legalDocumentId, DateTimeOffset acceptedAtUtc)
        : base(Guid.CreateVersion7())
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        }

        if (legalDocumentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Legal document ID cannot be empty.",
                nameof(legalDocumentId));
        }

        UserId = userId;
        LegalDocumentId = legalDocumentId;
        AcceptedAtUtc = acceptedAtUtc;
    }

    public Guid UserId { get; private set; }

    public Guid LegalDocumentId { get; private set; }

    public DateTimeOffset AcceptedAtUtc { get; private set; }

    public ApplicationUser User { get; private set; } = null!;

    public LegalDocument LegalDocument { get; private set; } = null!;
}
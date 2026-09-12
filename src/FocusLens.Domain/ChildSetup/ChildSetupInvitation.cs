using FocusLens.Domain.Common;

namespace FocusLens.Domain.ChildSetup;

public sealed class ChildSetupInvitation : AuditableEntity
{
    private ChildSetupInvitation()
    {
    }

    public ChildSetupInvitation(
        Guid childSetupDraftId,
        string targetEmailNormalized,
        string tokenHash,
        DateTimeOffset expiresAtUtc)
        : base(Guid.CreateVersion7())
    {
        if (childSetupDraftId == Guid.Empty)
        {
            throw new ArgumentException(
                "Child setup draft ID cannot be empty.",
                nameof(childSetupDraftId));
        }

        if (string.IsNullOrWhiteSpace(targetEmailNormalized))
        {
            throw new ArgumentException(
                "Target email is required.",
                nameof(targetEmailNormalized));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException(
                "Token hash is required.",
                nameof(tokenHash));
        }

        ChildSetupDraftId = childSetupDraftId;
        TargetEmailNormalized = targetEmailNormalized;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        Status = ChildSetupInvitationStatus.Pending;
    }

    public Guid ChildSetupDraftId { get; private set; }

    public string TargetEmailNormalized { get; private set; }
        = string.Empty;

    public string TokenHash { get; private set; }
        = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public ChildSetupInvitationStatus Status { get; private set; }

    public DateTimeOffset? ClaimedAtUtc { get; private set; }

    public ChildSetupDraft ChildSetupDraft { get; private set; } = null!;

    public bool IsExpired(DateTimeOffset now)
    {
        return Status == ChildSetupInvitationStatus.Pending
               && ExpiresAtUtc <= now;
    }

    public void Claim(DateTimeOffset now)
    {
        if (Status != ChildSetupInvitationStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only a pending invitation can be claimed.");
        }

        if (IsExpired(now))
        {
            throw new InvalidOperationException(
                "An expired invitation cannot be claimed.");
        }

        Status = ChildSetupInvitationStatus.Claimed;
        ClaimedAtUtc = now;
    }

    public void Cancel()
    {
        if (Status != ChildSetupInvitationStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only a pending invitation can be cancelled.");
        }

        Status = ChildSetupInvitationStatus.Cancelled;
    }

    public void Renew(
        string tokenHash,
        DateTimeOffset expiresAtUtc)
    {
        if (Status != ChildSetupInvitationStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only a pending invitation can be renewed.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException(
                "Token hash is required.",
                nameof(tokenHash));
        }

        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }
}
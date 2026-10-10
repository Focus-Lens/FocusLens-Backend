using FocusLens.Domain.Common;

namespace FocusLens.Domain.ChildSetup;

public sealed class ChildSetupInvitation : AuditableEntity
{
    private ChildSetupInvitation() { }

    public ChildSetupInvitation(
        Guid childSetupDraftId,
        string targetEmailNormalized,
        string tokenHash,
        DateTimeOffset expiresAtUtc,
        string? protectedToken = null
    )
        : this(
            childSetupDraftId,
            ChildSetupInvitationType.Email,
            targetEmailNormalized,
            tokenHash,
            expiresAtUtc,
            protectedToken
        )
    {
    }

    public ChildSetupInvitation(
        Guid childSetupDraftId,
        ChildSetupInvitationType type,
        string? targetEmailNormalized,
        string tokenHash,
        DateTimeOffset expiresAtUtc,
        string? protectedToken = null
    )
        : base(Guid.CreateVersion7())
    {
        if (childSetupDraftId == Guid.Empty)
        {
            throw new ArgumentException(
                "Child setup draft ID cannot be empty.",
                nameof(childSetupDraftId)
            );
        }

        if (
            type == ChildSetupInvitationType.Email
            && string.IsNullOrWhiteSpace(targetEmailNormalized)
        )
        {
            throw new ArgumentException("Target email is required.", nameof(targetEmailNormalized));
        }

        if (
            type == ChildSetupInvitationType.Link
            && !string.IsNullOrWhiteSpace(targetEmailNormalized)
        )
        {
            throw new ArgumentException(
                "Link invitations cannot have a target email.",
                nameof(targetEmailNormalized)
            );
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        }

        ChildSetupDraftId = childSetupDraftId;
        Type = type;
        TargetEmailNormalized = targetEmailNormalized;
        TokenHash = tokenHash;
        ProtectedToken = protectedToken;
        ExpiresAtUtc = expiresAtUtc;
        Status = ChildSetupInvitationStatus.Pending;
    }

    public Guid ChildSetupDraftId { get; private set; }

    public ChildSetupInvitationType Type { get; private set; }

    public string? TargetEmailNormalized { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public string? ProtectedToken { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public ChildSetupInvitationStatus Status { get; private set; }

    public DateTimeOffset? ClaimedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public ChildSetupDraft ChildSetupDraft { get; private set; } = null!;

    public bool IsExpired(DateTimeOffset now) => Status == ChildSetupInvitationStatus.Pending && ExpiresAtUtc <= now;

    public void Claim(DateTimeOffset now)
    {
        if (Status != ChildSetupInvitationStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending invitation can be claimed.");
        }

        if (IsExpired(now))
        {
            throw new InvalidOperationException("An expired invitation cannot be claimed.");
        }

        Status = ChildSetupInvitationStatus.Claimed;
        ClaimedAtUtc = now;
        ProtectedToken = null;
    }

    public void Cancel()
    {
        if (Status != ChildSetupInvitationStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending invitation can be cancelled.");
        }

        Status = ChildSetupInvitationStatus.Cancelled;
        ProtectedToken = null;
    }

    public void Renew(string tokenHash, DateTimeOffset expiresAtUtc, string? protectedToken = null)
    {
        if (Status != ChildSetupInvitationStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending invitation can be renewed.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        }

        TokenHash = tokenHash;
        ProtectedToken = protectedToken;
        ExpiresAtUtc = expiresAtUtc;
    }
}
using FocusLens.Domain.Common;

namespace FocusLens.Domain.Access;

/// <summary>
///     A student-owned invitation for a parent who may not have a FocusLens
///     account yet. The raw link token is never persisted.
/// </summary>
public sealed class StudentParentInvitation : AuditableEntity
{
    private StudentParentInvitation() { }

    public StudentParentInvitation(
        Guid studentId,
        string targetEmailNormalized,
        string tokenHash,
        DateTimeOffset expiresAtUtc)
        : base(Guid.CreateVersion7())
    {
        if (studentId == Guid.Empty)
        {
            throw new ArgumentException("Student ID cannot be empty.", nameof(studentId));
        }

        if (string.IsNullOrWhiteSpace(targetEmailNormalized))
        {
            throw new ArgumentException("Target email is required.", nameof(targetEmailNormalized));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        }

        StudentId = studentId;
        TargetEmailNormalized = targetEmailNormalized;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        Status = ParentInvitationStatus.Pending;
    }

    public Guid StudentId { get; private set; }

    public string TargetEmailNormalized { get; private set; } = string.Empty;

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public ParentInvitationStatus Status { get; private set; }

    public DateTimeOffset? RespondedAtUtc { get; private set; }

    public Student Student { get; private set; } = null!;

    public bool IsExpired(DateTimeOffset now) => Status == ParentInvitationStatus.Pending && ExpiresAtUtc <= now;

    public void Accept(DateTimeOffset now) => Respond(ParentInvitationStatus.Accepted, now);

    public void Decline(DateTimeOffset now) => Respond(ParentInvitationStatus.Declined, now);

    public void Cancel(DateTimeOffset now) => Respond(ParentInvitationStatus.Cancelled, now);

    public void Renew(string tokenHash, DateTimeOffset expiresAtUtc)
    {
        if (Status != ParentInvitationStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending invitation can be renewed.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        }

        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    private void Respond(ParentInvitationStatus newStatus, DateTimeOffset now)
    {
        if (Status != ParentInvitationStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending invitation can be updated.");
        }

        if (IsExpired(now))
        {
            throw new InvalidOperationException("An expired invitation cannot be updated.");
        }

        Status = newStatus;
        RespondedAtUtc = now;
    }
}
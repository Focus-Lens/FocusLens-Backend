using FocusLens.Domain.Common;

namespace FocusLens.Domain.Access;

public class ParentStudentRelationship : AuditableEntity
{
    private ParentStudentRelationship() { }

    public ParentStudentRelationship(Guid parentId, Guid studentId)
        : this(parentId, studentId, InvitationInitiator.Parent, null)
    {
    }

    public ParentStudentRelationship(
        Guid parentId,
        Guid studentId,
        InvitationInitiator initiatedBy,
        DateTimeOffset? expiresAtUtc)
        : base(Guid.CreateVersion7())
    {
        if (parentId == Guid.Empty)
        {
            throw new ArgumentException("Parent ID cannot be empty.", nameof(parentId));
        }

        if (studentId == Guid.Empty)
        {
            throw new ArgumentException("Student ID cannot be empty.", nameof(studentId));
        }

        ParentId = parentId;
        StudentId = studentId;
        InitiatedBy = initiatedBy;
        ExpiresAtUtc = expiresAtUtc;
        Status = RelationshipStatus.Pending;
    }

    public Guid ParentId { get; private set; }

    public Guid StudentId { get; private set; }

    public RelationshipStatus Status { get; private set; }

    public InvitationInitiator InitiatedBy { get; private set; }

    /// <summary>
    /// The deadline for acting on a pending invitation. Active or historical
    /// relationships retain this value for audit purposes.
    /// </summary>
    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public Parent Parent { get; private set; } = null!;

    public Student Student { get; private set; } = null!;

    public bool IsExpired(DateTimeOffset now)
        => Status == RelationshipStatus.Pending &&
           ExpiresAtUtc.HasValue &&
           ExpiresAtUtc.Value <= now;

    public void Accept()
        => Accept(DateTimeOffset.UtcNow);

    public void Accept(DateTimeOffset now)
    {
        if (Status != RelationshipStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only a pending relationship can be accepted.");
        }

        if (IsExpired(now))
        {
            throw new InvalidOperationException("An expired invitation cannot be accepted.");
        }

        Status = RelationshipStatus.Active;
        RevokedAtUtc = null;
    }

    public void Reject()
    {
        if (Status != RelationshipStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only a pending relationship can be rejected.");
        }

        Status = RelationshipStatus.Revoked;
        RevokedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Revoke()
    {
        if (Status != RelationshipStatus.Active)
        {
            throw new InvalidOperationException(
                "Only an active relationship can be revoked.");
        }

        Status = RelationshipStatus.Revoked;
        RevokedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Reinvite()
        => Reinvite(null);

    public void Reinvite(DateTimeOffset? expiresAtUtc)
    {
        if (Status != RelationshipStatus.Revoked)
        {
            throw new InvalidOperationException(
                "Only a revoked relationship can be re-invited.");
        }

        Status = RelationshipStatus.Pending;
        RevokedAtUtc = null;
        ExpiresAtUtc = expiresAtUtc;
    }
}

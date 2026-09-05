using FocusLens.Domain.Common;

namespace FocusLens.Domain.Access;

public class ParentStudentRelationship : AuditableEntity
{
    private ParentStudentRelationship() { }

    public ParentStudentRelationship(Guid parentId, Guid studentId)
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
        Status = RelationshipStatus.Pending;
    }

    public Guid ParentId { get; private set; }

    public Guid StudentId { get; private set; }

    public RelationshipStatus Status { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public Parent Parent { get; private set; } = null!;

    public Student Student { get; private set; } = null!;

    public void Accept()
    {
        if (Status != RelationshipStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only a pending relationship can be accepted.");
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
    {
        if (Status != RelationshipStatus.Revoked)
        {
            throw new InvalidOperationException(
                "Only a revoked relationship can be re-invited.");
        }

        Status = RelationshipStatus.Pending;
        RevokedAtUtc = null;
    }
}

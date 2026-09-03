using FocusLens.Domain.Access;

namespace FocusLens.Domain.UnitTests.Access;

public class ParentStudentRelationshipTests
{
    [Fact]
    public void Create_WithValidIds_StartsAsPending()
    {
        Guid parentId = Guid.NewGuid();
        Guid studentId = Guid.NewGuid();

        ParentStudentRelationship relationship =
            new(parentId, studentId);

        Assert.Equal(parentId, relationship.ParentId);
        Assert.Equal(studentId, relationship.StudentId);
        Assert.Equal(RelationshipStatus.Pending, relationship.Status);
        Assert.Null(relationship.RevokedAtUtc);
    }

    [Fact]
    public void Create_WithEmptyParentId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new ParentStudentRelationship(Guid.Empty, Guid.NewGuid()));
    }

    [Fact]
    public void Create_WithEmptyStudentId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new ParentStudentRelationship(Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void Accept_WhenPending_MakesRelationshipActive()
    {
        ParentStudentRelationship relationship =
            new(Guid.NewGuid(), Guid.NewGuid());

        relationship.Accept();

        Assert.Equal(RelationshipStatus.Active, relationship.Status);
        Assert.Null(relationship.RevokedAtUtc);
    }

    [Fact]
    public void Accept_WhenNotPending_ThrowsInvalidOperationException()
    {
        ParentStudentRelationship relationship =
            new(Guid.NewGuid(), Guid.NewGuid());

        relationship.Accept();

        Assert.Throws<InvalidOperationException>(() => relationship.Accept());
    }

    [Fact]
    public void Reject_WhenPending_MakesRelationshipRevoked()
    {
        ParentStudentRelationship relationship =
            new(Guid.NewGuid(), Guid.NewGuid());

        relationship.Reject();

        Assert.Equal(RelationshipStatus.Revoked, relationship.Status);
        Assert.NotNull(relationship.RevokedAtUtc);
    }

    [Fact]
    public void Reject_WhenNotPending_ThrowsInvalidOperationException()
    {
        ParentStudentRelationship relationship =
            new(Guid.NewGuid(), Guid.NewGuid());

        relationship.Accept();

        Assert.Throws<InvalidOperationException>(() => relationship.Reject());
    }

    [Fact]
    public void Revoke_WhenActive_MakesRelationshipRevoked()
    {
        ParentStudentRelationship relationship =
            new(Guid.NewGuid(), Guid.NewGuid());

        relationship.Accept();
        relationship.Revoke();

        Assert.Equal(RelationshipStatus.Revoked, relationship.Status);
        Assert.NotNull(relationship.RevokedAtUtc);
    }

    [Fact]
    public void Revoke_WhenPending_ThrowsInvalidOperationException()
    {
        ParentStudentRelationship relationship =
            new(Guid.NewGuid(), Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => relationship.Revoke());
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_ThrowsInvalidOperationException()
    {
        ParentStudentRelationship relationship =
            new(Guid.NewGuid(), Guid.NewGuid());

        relationship.Accept();
        relationship.Revoke();

        Assert.Throws<InvalidOperationException>(() => relationship.Revoke());
    }
}

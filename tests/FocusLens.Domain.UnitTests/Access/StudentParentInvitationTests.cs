using FocusLens.Domain.Access;

namespace FocusLens.Domain.UnitTests.Access;

public sealed class StudentParentInvitationTests
{
    [Fact]
    public void Constructor_EmailInvitation_StoresEmailType()
    {
        StudentParentInvitation invitation = new(
            Guid.NewGuid(),
            "PARENT@EXAMPLE.COM",
            "HASH",
            DateTimeOffset.UtcNow.AddDays(7));

        Assert.Equal(StudentParentInvitationType.Email, invitation.Type);
        Assert.Equal("PARENT@EXAMPLE.COM", invitation.TargetEmailNormalized);
    }

    [Fact]
    public void Constructor_LinkInvitation_AllowsNoTargetEmail()
    {
        StudentParentInvitation invitation = new(
            Guid.NewGuid(),
            StudentParentInvitationType.Link,
            null,
            "HASH",
            DateTimeOffset.UtcNow.AddDays(7));

        Assert.Equal(StudentParentInvitationType.Link, invitation.Type);
        Assert.Null(invitation.TargetEmailNormalized);
    }

    [Fact]
    public void Constructor_LinkInvitation_WithTargetEmail_Throws()
    {
        Assert.Throws<ArgumentException>(() => new StudentParentInvitation(
            Guid.NewGuid(),
            StudentParentInvitationType.Link,
            "PARENT@EXAMPLE.COM",
            "HASH",
            DateTimeOffset.UtcNow.AddDays(7)));
    }
}

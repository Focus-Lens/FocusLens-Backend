using FocusLens.Domain.ChildSetup;

namespace FocusLens.Domain.UnitTests.ChildSetup;

public class ChildSetupInvitationTests
{
    [Fact]
    public void Constructor_WithValidValues_CreatesPendingInvitation()
    {
        Guid draftId = Guid.NewGuid();
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddDays(7);

        ChildSetupInvitation invitation =
            new(
                draftId,
                "CHILD@EXAMPLE.COM",
                "HASH",
                expiresAt);

        Assert.Equal(draftId, invitation.ChildSetupDraftId);
        Assert.Equal(
            "CHILD@EXAMPLE.COM",
            invitation.TargetEmailNormalized);
        Assert.Equal("HASH", invitation.TokenHash);
        Assert.Null(invitation.ProtectedToken);
        Assert.Equal(expiresAt, invitation.ExpiresAtUtc);
        Assert.Equal(
            ChildSetupInvitationStatus.Pending,
            invitation.Status);
        Assert.Null(invitation.ClaimedAtUtc);
    }

    [Fact]
    public void Claim_WhilePending_MovesToClaimed()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        ChildSetupInvitation invitation =
            new(
                Guid.NewGuid(),
                "CHILD@EXAMPLE.COM",
                "HASH",
                now.AddDays(7));

        invitation.Claim(now);

        Assert.Equal(
            ChildSetupInvitationStatus.Claimed,
            invitation.Status);
        Assert.Equal(now, invitation.ClaimedAtUtc);
    }

    [Fact]
    public void Claim_WhenExpired_Throws()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        ChildSetupInvitation invitation =
            new(
                Guid.NewGuid(),
                "CHILD@EXAMPLE.COM",
                "HASH",
                now.AddMinutes(-1));

        Assert.Throws<InvalidOperationException>(() => invitation.Claim(now));

        Assert.Equal(
            ChildSetupInvitationStatus.Pending,
            invitation.Status);
    }

    [Fact]
    public void Cancel_WhilePending_MovesToCancelled()
    {
        ChildSetupInvitation invitation =
            new(
                Guid.NewGuid(),
                "CHILD@EXAMPLE.COM",
                "HASH",
                DateTimeOffset.UtcNow.AddDays(7));

        invitation.Cancel();

        Assert.Equal(
            ChildSetupInvitationStatus.Cancelled,
            invitation.Status);
    }

    [Fact]
    public void Renew_WhilePending_ReplacesTokenAndExpiry()
    {
        ChildSetupInvitation invitation =
            new(
                Guid.NewGuid(),
                "CHILD@EXAMPLE.COM",
                "OLD_HASH",
                DateTimeOffset.UtcNow.AddDays(1));

        DateTimeOffset newExpiry =
            DateTimeOffset.UtcNow.AddDays(7);

        invitation.Renew("NEW_HASH", newExpiry, "PROTECTED_TOKEN");

        Assert.Equal("NEW_HASH", invitation.TokenHash);
        Assert.Equal("PROTECTED_TOKEN", invitation.ProtectedToken);
        Assert.Equal(newExpiry, invitation.ExpiresAtUtc);
    }

    [Fact]
    public void Claim_AfterCancelled_Throws()
    {
        ChildSetupInvitation invitation =
            new(
                Guid.NewGuid(),
                "CHILD@EXAMPLE.COM",
                "HASH",
                DateTimeOffset.UtcNow.AddDays(7));

        invitation.Cancel();

        Assert.Throws<InvalidOperationException>(() => invitation.Claim(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Renew_AfterClaimed_Throws()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        ChildSetupInvitation invitation =
            new(
                Guid.NewGuid(),
                "CHILD@EXAMPLE.COM",
                "HASH",
                now.AddDays(7));

        invitation.Claim(now);

        Assert.Throws<InvalidOperationException>(() => invitation.Renew(
            "NEW_HASH",
            now.AddDays(14)));
    }

    [Fact]
    public void MarkInvitationCancelled_ForInvitedDraft_ReturnsToDraft()
    {
        ChildSetupDraft draft = new(Guid.NewGuid());
        draft.SetName("Karim", "Mahmoud");
        draft.MarkInvited();

        draft.MarkInvitationCancelled();

        Assert.Equal(ChildSetupStatus.Draft, draft.Status);
        Assert.Equal("Karim", draft.FirstName);
        Assert.Equal("Mahmoud", draft.LastName);
    }
}
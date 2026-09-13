using System.Security.Cryptography;
using System.Text;
using FocusLens.Application.ChildSetup;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Application.UnitTests.ChildSetup;

public sealed class ResendChildSetupInvitationCommandHandlerTests
{
    [Fact]
    public async Task Resend_WithPendingInvitation_RenewsInvitationAndEmailsChild()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = CreateInvitedDraft(parent.Id);

        string oldHash = Hash("old-token");
        ChildSetupInvitation invitation = new(
            draft.Id,
            "CHILD@EXAMPLE.COM",
            oldHash,
            DateTimeOffset.UtcNow.AddDays(1));

        InMemoryRepository<ChildSetupInvitation> invitations =
            new(invitation);

        FakeEmailSender emailSender = new();

        ResendChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            invitations,
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            emailSender,
            new FakeInvitationUrlBuilder(),
            TimeProvider.System);

        DateTimeOffset oldExpiry = invitation.ExpiresAtUtc;

        Result<ChildSetupInvitationResponse> result =
            await handler.Handle(
                new ResendChildSetupInvitationCommand(draft.Id),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value.Status);
        Assert.Equal(invitation.Id, result.Value.Id);
        Assert.NotEqual(oldHash, invitation.TokenHash);
        Assert.NotEqual(oldExpiry, invitation.ExpiresAtUtc);
        Assert.Contains(
            "/invitations/child-setup/",
            result.Value.InvitationUrl);

        Assert.Single(emailSender.ChildSetupInvitations);
        Assert.Equal(
            "CHILD@EXAMPLE.COM",
            emailSender.ChildSetupInvitations[0].ChildEmail);
        Assert.Equal(
            result.Value.InvitationUrl,
            emailSender.ChildSetupInvitations[0].InvitationUrl);

        string token =
            result.Value.InvitationUrl.Split((char)47).Last();

        Assert.Equal(Hash(token), invitation.TokenHash);
        Assert.NotEqual(token, invitation.TokenHash);
    }

    [Fact]
    public async Task Resend_WithDraftOwnedByAnotherParent_ReturnsNotFound()
    {
        Parent owner = new(Guid.NewGuid());
        Parent currentParent = new(Guid.NewGuid());
        ChildSetupDraft draft = CreateInvitedDraft(owner.Id);

        ChildSetupInvitation invitation = new(
            draft.Id,
            "child@example.com",
            Hash("token"),
            DateTimeOffset.UtcNow.AddDays(1));

        FakeEmailSender emailSender = new();

        ResendChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(currentParent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new FakeCurrentUser(currentParent.UserId),
            new FakeUnitOfWork(),
            emailSender,
            new FakeInvitationUrlBuilder(),
            TimeProvider.System);

        Result<ChildSetupInvitationResponse> result =
            await handler.Handle(
                new ResendChildSetupInvitationCommand(draft.Id),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetup.NotFound", result.TopError.Code);
        Assert.Empty(emailSender.ChildSetupInvitations);
    }

    [Fact]
    public async Task Resend_WithNoPendingInvitation_ReturnsNotFound()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = CreateInvitedDraft(parent.Id);

        FakeEmailSender emailSender = new();

        ResendChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ChildSetupInvitation>(),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            emailSender,
            new FakeInvitationUrlBuilder(),
            TimeProvider.System);

        Result<ChildSetupInvitationResponse> result =
            await handler.Handle(
                new ResendChildSetupInvitationCommand(draft.Id),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "ChildSetupInvitation.NotFound",
            result.TopError.Code);
        Assert.Empty(emailSender.ChildSetupInvitations);
    }

    private static ChildSetupDraft CreateInvitedDraft(Guid parentId)
    {
        ChildSetupDraft draft = new(parentId);
        draft.MarkInvited();
        return draft;
    }

    private static string Hash(string token)
    {
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}

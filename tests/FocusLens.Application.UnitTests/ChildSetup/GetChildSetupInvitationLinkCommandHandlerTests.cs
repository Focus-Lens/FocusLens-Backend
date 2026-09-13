using System.Security.Cryptography;
using System.Text;
using FocusLens.Application.ChildSetup;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Application.UnitTests.ChildSetup;

public sealed class GetChildSetupInvitationLinkCommandHandlerTests
{
    [Fact]
    public async Task GetLink_WithPendingInvitation_RenewsInvitationAndReturnsLink()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = CreateInvitedDraft(parent.Id);

        string oldHash = Hash("old-token");
        ChildSetupInvitation invitation = new(
            draft.Id,
            "child@example.com",
            oldHash,
            DateTimeOffset.UtcNow.AddDays(1));

        InMemoryRepository<ChildSetupInvitation> invitations =
            new(invitation);

        GetChildSetupInvitationLinkCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            invitations,
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FakeInvitationUrlBuilder(),
            TimeProvider.System);

        DateTimeOffset oldExpiry = invitation.ExpiresAtUtc;

        Result<ChildSetupInvitationResponse> result =
            await handler.Handle(
                new GetChildSetupInvitationLinkCommand(draft.Id),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(invitation.Id, result.Value.Id);
        Assert.Equal("Pending", result.Value.Status);
        Assert.Contains(
            "/invitations/child-setup/",
            result.Value.InvitationUrl);

        Assert.NotEqual(oldHash, invitation.TokenHash);
        Assert.NotEqual(oldExpiry, invitation.ExpiresAtUtc);

        string token =
            result.Value.InvitationUrl.Split((char)47).Last();

        Assert.Equal(Hash(token), invitation.TokenHash);
        Assert.NotEqual(token, invitation.TokenHash);
    }

    [Fact]
    public async Task GetLink_WithDraftOwnedByAnotherParent_ReturnsNotFound()
    {
        Parent owner = new(Guid.NewGuid());
        Parent currentParent = new(Guid.NewGuid());
        ChildSetupDraft draft = CreateInvitedDraft(owner.Id);

        ChildSetupInvitation invitation = new(
            draft.Id,
            "child@example.com",
            Hash("token"),
            DateTimeOffset.UtcNow.AddDays(1));

        GetChildSetupInvitationLinkCommandHandler handler = new(
            new InMemoryRepository<Parent>(currentParent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new FakeCurrentUser(currentParent.UserId),
            new FakeUnitOfWork(),
            new FakeInvitationUrlBuilder(),
            TimeProvider.System);

        Result<ChildSetupInvitationResponse> result =
            await handler.Handle(
                new GetChildSetupInvitationLinkCommand(draft.Id),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetup.NotFound", result.TopError.Code);
    }

    [Fact]
    public async Task GetLink_WithNoPendingInvitation_ReturnsNotFound()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = CreateInvitedDraft(parent.Id);

        GetChildSetupInvitationLinkCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ChildSetupInvitation>(),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FakeInvitationUrlBuilder(),
            TimeProvider.System);

        Result<ChildSetupInvitationResponse> result =
            await handler.Handle(
                new GetChildSetupInvitationLinkCommand(draft.Id),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "ChildSetupInvitation.NotFound",
            result.TopError.Code);
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

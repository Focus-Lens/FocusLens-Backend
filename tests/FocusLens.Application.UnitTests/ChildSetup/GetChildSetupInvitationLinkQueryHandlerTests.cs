using System.Security.Cryptography;
using System.Text;
using FocusLens.Application.ChildSetup;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Application.UnitTests.ChildSetup;

public sealed class GetChildSetupInvitationLinkQueryHandlerTests
{
    [Fact]
    public async Task GetLink_WithPendingInvitation_ReturnsLatestProtectedTokenUrl()
    {
        const string oldToken = "old-token";
        const string latestToken = "latest-token";
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = new(parent.Id);
        draft.MarkInvited();

        ChildSetupInvitation invitation = new(
            draft.Id,
            "CHILD@EXAMPLE.COM",
            Hash(oldToken),
            DateTimeOffset.UtcNow.AddDays(1),
            $"protected:{oldToken}");
        invitation.Renew(
            Hash(latestToken),
            DateTimeOffset.UtcNow.AddDays(7),
            $"protected:{latestToken}");

        GetChildSetupInvitationLinkQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new FakeCurrentUser(parentUserId),
            new FakeInvitationUrlBuilder(),
            new FakeChildSetupInvitationTokenProtector(),
            TimeProvider.System);

        Result<ChildSetupInvitationLinkResponse> result = await handler.Handle(
            new GetChildSetupInvitationLinkQuery(draft.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            $"https://child.focuslens.test/invitations/child-setup/{latestToken}",
            result.Value.InvitationUrl);
        Assert.Equal(Hash(latestToken), invitation.TokenHash);
    }

    [Fact]
    public async Task GetLink_WithCancelledInvitation_ReturnsConflict()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = new(parent.Id);
        draft.MarkInvited();
        ChildSetupInvitation invitation = new(
            draft.Id,
            "CHILD@EXAMPLE.COM",
            Hash("token"),
            DateTimeOffset.UtcNow.AddDays(1),
            "protected:token");
        invitation.Cancel();

        GetChildSetupInvitationLinkQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new FakeCurrentUser(parentUserId),
            new FakeInvitationUrlBuilder(),
            new FakeChildSetupInvitationTokenProtector(),
            TimeProvider.System);

        Result<ChildSetupInvitationLinkResponse> result = await handler.Handle(
            new GetChildSetupInvitationLinkQuery(draft.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetupInvitation.NotFound", result.TopError.Code);
    }

    [Fact]
    public async Task GetLink_WhenProtectedTokenIsMissing_ReturnsLinkUnavailable()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = new(parent.Id);
        draft.MarkInvited();
        ChildSetupInvitation invitation = new(
            draft.Id,
            "CHILD@EXAMPLE.COM",
            Hash("token"),
            DateTimeOffset.UtcNow.AddDays(1));

        GetChildSetupInvitationLinkQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new FakeCurrentUser(parentUserId),
            new FakeInvitationUrlBuilder(),
            new FakeChildSetupInvitationTokenProtector(),
            TimeProvider.System);

        Result<ChildSetupInvitationLinkResponse> result = await handler.Handle(
            new GetChildSetupInvitationLinkQuery(draft.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetupInvitation.LinkUnavailable", result.TopError.Code);
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

using System.Security.Cryptography;
using System.Text;
using FocusLens.Application.ChildSetup;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Application.UnitTests.ChildSetup;

public sealed class RegenerateChildSetupInvitationLinkCommandHandlerTests
{
    [Fact]
    public async Task RegenerateLink_WithPendingInvitation_RenewsInvitationAndReturnsLink()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = CreateInvitedDraft(parent.Id);

        string oldHash = Hash("old-token");
        ChildSetupInvitation invitation = new(
            draft.Id,
            ChildSetupInvitationType.Link,
            null,
            oldHash,
            DateTimeOffset.UtcNow.AddDays(1)
        );

        InMemoryRepository<ChildSetupInvitation> invitations = new(invitation);

        RegenerateChildSetupInvitationLinkCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            invitations,
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FakeInvitationUrlBuilder(),
            new FakeChildSetupInvitationTokenProtector(),
            TimeProvider.System
        );

        DateTimeOffset oldExpiry = invitation.ExpiresAtUtc;

        Result<ChildSetupInvitationResponse> result = await handler.Handle(
            new RegenerateChildSetupInvitationLinkCommand(draft.Id),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(invitation.Id, result.Value.Id);
        Assert.Equal("Pending", result.Value.Status);
        Assert.Contains("/invitations/child-setup/", result.Value.InvitationUrl);

        Assert.NotEqual(oldHash, invitation.TokenHash);
        Assert.NotEqual(oldExpiry, invitation.ExpiresAtUtc);

        string token = result.Value.InvitationUrl.Split((char)47).Last();

        Assert.Equal(Hash(token), invitation.TokenHash);
        Assert.NotEqual(token, invitation.TokenHash);
    }

    [Fact]
    public async Task RegenerateLink_WithDraftOwnedByAnotherParent_ReturnsNotFound()
    {
        Parent owner = new(Guid.NewGuid());
        Parent currentParent = new(Guid.NewGuid());
        ChildSetupDraft draft = CreateInvitedDraft(owner.Id);

        ChildSetupInvitation invitation = new(
            draft.Id,
            ChildSetupInvitationType.Link,
            null,
            Hash("token"),
            DateTimeOffset.UtcNow.AddDays(1)
        );

        RegenerateChildSetupInvitationLinkCommandHandler handler = new(
            new InMemoryRepository<Parent>(currentParent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new FakeCurrentUser(currentParent.UserId),
            new FakeUnitOfWork(),
            new FakeInvitationUrlBuilder(),
            new FakeChildSetupInvitationTokenProtector(),
            TimeProvider.System
        );

        Result<ChildSetupInvitationResponse> result = await handler.Handle(
            new RegenerateChildSetupInvitationLinkCommand(draft.Id),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetup.NotFound", result.TopError.Code);
    }

    [Fact]
    public async Task RegenerateLink_WithNoPendingInvitation_ReturnsNotFound()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = CreateInvitedDraft(parent.Id);

        RegenerateChildSetupInvitationLinkCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ChildSetupInvitation>(),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FakeInvitationUrlBuilder(),
            new FakeChildSetupInvitationTokenProtector(),
            TimeProvider.System
        );

        Result<ChildSetupInvitationResponse> result = await handler.Handle(
            new RegenerateChildSetupInvitationLinkCommand(draft.Id),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetupInvitation.NotFound", result.TopError.Code);
    }

    [Fact]
    public async Task RegenerateLink_WithEmptyDraftId_ReturnsValidationError()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);

        RegenerateChildSetupInvitationLinkCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(),
            new InMemoryRepository<ChildSetupInvitation>(),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FakeInvitationUrlBuilder(),
            new FakeChildSetupInvitationTokenProtector(),
            TimeProvider.System
        );

        Result<ChildSetupInvitationResponse> result = await handler.Handle(
            new RegenerateChildSetupInvitationLinkCommand(Guid.Empty),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetup.InvalidDraftId", result.TopError.Code);
    }

    [Fact]
    public async Task RegenerateLink_WithDraftNotInvited_ReturnsConflict()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = new(parent.Id);

        RegenerateChildSetupInvitationLinkCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ChildSetupInvitation>(),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FakeInvitationUrlBuilder(),
            new FakeChildSetupInvitationTokenProtector(),
            TimeProvider.System
        );

        Result<ChildSetupInvitationResponse> result = await handler.Handle(
            new RegenerateChildSetupInvitationLinkCommand(draft.Id),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetup.NotInvited", result.TopError.Code);
    }

    [Fact]
    public async Task RegenerateLink_WithEmailInvitation_ReturnsConflict()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = CreateInvitedDraft(parent.Id);

        ChildSetupInvitation invitation = new(
            draft.Id,
            ChildSetupInvitationType.Email,
            "child@example.com",
            Hash("token"),
            DateTimeOffset.UtcNow.AddDays(1)
        );

        RegenerateChildSetupInvitationLinkCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FakeInvitationUrlBuilder(),
            new FakeChildSetupInvitationTokenProtector(),
            TimeProvider.System
        );

        Result<ChildSetupInvitationResponse> result = await handler.Handle(
            new RegenerateChildSetupInvitationLinkCommand(draft.Id),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "ChildSetupInvitation.EmailLinkCannotBeRegenerated",
            result.TopError.Code
        );
    }

    [Fact]
    public async Task RegenerateLink_WithExpiredInvitation_ReturnsConflict()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = CreateInvitedDraft(parent.Id);

        ChildSetupInvitation invitation = new(
            draft.Id,
            ChildSetupInvitationType.Link,
            null,
            Hash("token"),
            DateTimeOffset.UtcNow.AddMinutes(-1)
        );

        RegenerateChildSetupInvitationLinkCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FakeInvitationUrlBuilder(),
            new FakeChildSetupInvitationTokenProtector(),
            TimeProvider.System
        );

        Result<ChildSetupInvitationResponse> result = await handler.Handle(
            new RegenerateChildSetupInvitationLinkCommand(draft.Id),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetupInvitation.Expired", result.TopError.Code);
    }

    private static ChildSetupDraft CreateInvitedDraft(Guid parentId)
    {
        ChildSetupDraft draft = new(parentId);
        draft.MarkInvited();
        return draft;
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
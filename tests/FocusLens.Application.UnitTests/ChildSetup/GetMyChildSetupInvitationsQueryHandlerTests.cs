using FocusLens.Application.ChildSetup;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Students;

namespace FocusLens.Application.UnitTests.ChildSetup;

public sealed class GetMyChildSetupInvitationsQueryHandlerTests
{
    [Fact]
    public async Task GetMyInvitations_ReturnsOnlyCurrentParentsPendingInvitations()
    {
        Guid currentUserId = Guid.NewGuid();

        Parent currentParent = new(currentUserId);
        Parent anotherParent = new(Guid.NewGuid());

        ChildSetupDraft currentDraft = CreateDraft(currentParent.Id, "Youssef", "Mahmoud");
        ChildSetupDraft anotherDraft = CreateDraft(anotherParent.Id, "Mariam", "Ali");

        ChildSetupInvitation currentInvitation =
            CreateInvitation(currentDraft, "youssef@example.com");

        ChildSetupInvitation anotherInvitation =
            CreateInvitation(anotherDraft, "mariam@example.com");

        currentDraft.MarkInvited();
        anotherDraft.MarkInvited();

        currentInvitation.SetPrivateProperty("ChildSetupDraft", currentDraft);
        anotherInvitation.SetPrivateProperty("ChildSetupDraft", anotherDraft);

        GetMyChildSetupInvitationsQueryHandler handler = new(
            new InMemoryRepository<Parent>(currentParent, anotherParent),
            new InMemoryRepository<ChildSetupInvitation>(
                currentInvitation,
                anotherInvitation),
            new FakeCurrentUser(currentUserId));

        IReadOnlyList<ParentChildSetupInvitationResponse> result =
            await handler.Handle(
                new GetMyChildSetupInvitationsQuery(),
                CancellationToken.None);

        Assert.Single(result);

        ParentChildSetupInvitationResponse response = result.Single();

        Assert.Equal(currentDraft.Id, response.DraftId);
        Assert.Equal(currentInvitation.Id, response.InvitationId);
        Assert.Equal("Youssef", response.FirstName);
        Assert.Equal("Mahmoud", response.LastName);
        Assert.Equal("YOUSSEF@EXAMPLE.COM", response.TargetEmail);
        Assert.Equal("Pending", response.Status);
    }

    [Fact]
    public async Task GetMyInvitations_ExcludesNonPendingInvitations()
    {
        Guid currentUserId = Guid.NewGuid();
        Parent parent = new(currentUserId);

        ChildSetupDraft cancelledDraft = CreateDraft(parent.Id, "Cancelled", "Child");
        cancelledDraft.MarkInvited();

        ChildSetupInvitation cancelled =
            CreateInvitation(cancelledDraft, "cancelled@example.com");

        cancelled.SetPrivateProperty("ChildSetupDraft", cancelledDraft);
        cancelled.Cancel();

        ChildSetupDraft claimedDraft = CreateDraft(parent.Id, "Claimed", "Child");
        claimedDraft.MarkInvited();

        ChildSetupInvitation claimed =
            CreateInvitation(claimedDraft, "claimed@example.com");

        claimed.SetPrivateProperty("ChildSetupDraft", claimedDraft);
        claimed.Claim(DateTimeOffset.UtcNow);

        ChildSetupDraft pendingDraft = CreateDraft(parent.Id, "Pending", "Child");
        pendingDraft.MarkInvited();

        ChildSetupInvitation pending =
            CreateInvitation(pendingDraft, "pending@example.com");

        pending.SetPrivateProperty("ChildSetupDraft", pendingDraft);

        GetMyChildSetupInvitationsQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupInvitation>(
                cancelled,
                claimed,
                pending),
            new FakeCurrentUser(currentUserId));

        IReadOnlyList<ParentChildSetupInvitationResponse> result =
            await handler.Handle(
                new GetMyChildSetupInvitationsQuery(),
                CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(pending.Id, result[0].InvitationId);
    }

    [Fact]
    public async Task GetMyInvitations_WithoutParentProfile_ReturnsEmpty()
    {
        Guid userId = Guid.NewGuid();

        GetMyChildSetupInvitationsQueryHandler handler = new(
            new InMemoryRepository<Parent>(),
            new InMemoryRepository<ChildSetupInvitation>(),
            new FakeCurrentUser(userId));

        IReadOnlyList<ParentChildSetupInvitationResponse> result =
            await handler.Handle(
                new GetMyChildSetupInvitationsQuery(),
                CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetMyInvitations_WithoutCurrentUser_ReturnsEmpty()
    {
        GetMyChildSetupInvitationsQueryHandler handler = new(
            new InMemoryRepository<Parent>(),
            new InMemoryRepository<ChildSetupInvitation>(),
            new FakeCurrentUser(Guid.Empty));

        IReadOnlyList<ParentChildSetupInvitationResponse> result =
            await handler.Handle(
                new GetMyChildSetupInvitationsQuery(),
                CancellationToken.None);

        Assert.Empty(result);
    }

    private static ChildSetupDraft CreateDraft(
        Guid parentId,
        string firstName,
        string lastName)
    {
        ChildSetupDraft draft = new(parentId);
        draft.SetName(firstName, lastName);
        draft.SetGrade(StudentGrade.Grade10);
        return draft;
    }

    private static ChildSetupInvitation CreateInvitation(
        ChildSetupDraft draft,
        string email)
    {
        return new ChildSetupInvitation(
            draft.Id,
            email.ToUpperInvariant(),
            "test-hash",
            DateTimeOffset.UtcNow.AddDays(7));
    }
}
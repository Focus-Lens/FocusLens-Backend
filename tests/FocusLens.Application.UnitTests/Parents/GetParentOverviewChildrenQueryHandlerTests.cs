using FocusLens.Application.Parents;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Identity;

namespace FocusLens.Application.UnitTests.Parents;

public sealed class GetParentOverviewChildrenQueryHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_ReturnsActiveChildrenDraftsAndPendingSetupInvitationsForCurrentParent()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        Parent otherParent = new(Guid.NewGuid());

        Student activeStudent = new(Guid.NewGuid());
        activeStudent.SetPrivateProperty(
            "User",
            new ApplicationUser { FirstName = "Active", LastName = "Student" });

        ParentStudentRelationship activeRelationship =
            new(parent.Id, activeStudent.Id);
        activeRelationship.Accept();

        ChildSetupDraft draft = new(parent.Id);
        draft.SetName("Draft", "Child");

        ChildSetupDraft invitedDraft = new(parent.Id);
        invitedDraft.SetName("Invited", "Child");
        invitedDraft.MarkInvited();
        ChildSetupInvitation invitation = new(
            invitedDraft.Id,
            "CHILD@EXAMPLE.COM",
            "HASH",
            new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));

        ChildSetupDraft otherParentDraft = new(otherParent.Id);
        otherParentDraft.SetName("Other", "Child");

        GetParentOverviewChildrenQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent, otherParent),
            new InMemoryRepository<ParentStudentRelationship>(activeRelationship),
            new InMemoryRepository<Student>(activeStudent),
            new InMemoryRepository<ChildSetupDraft>(draft, invitedDraft, otherParentDraft),
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new FakeCurrentUser(parentUserId),
            new FixedTimeProvider(Now));

        IReadOnlyList<ParentOverviewChildResponse> result = await handler.Handle(
            new GetParentOverviewChildrenQuery(),
            CancellationToken.None);

        Assert.Equal(3, result.Count);

        Assert.Contains(result, child =>
            child.Type == "ActiveChild" &&
            child.Status == "Active" &&
            child.FirstName == "Active" &&
            child.LastName == "Student" &&
            child.StudentId == activeStudent.Id &&
            child.ChildSetupDraftId is null);

        Assert.Contains(result, child =>
            child.Type == "ChildSetup" &&
            child.Status == "Draft" &&
            child.FirstName == "Draft" &&
            child.LastName == "Child" &&
            child.ChildSetupDraftId == draft.Id &&
            child.ChildSetupInvitationId is null);

        Assert.Contains(result, child =>
            child.Type == "ChildSetup" &&
            child.Status == "Invited" &&
            child.FirstName == "Invited" &&
            child.LastName == "Child" &&
            child.ChildSetupDraftId == invitedDraft.Id &&
            child.ChildSetupInvitationId == invitation.Id &&
            child.ChildSetupInvitationStatus == "Pending" &&
            child.ChildSetupInvitationExpiresAtUtc == invitation.ExpiresAtUtc);

        Assert.DoesNotContain(result, child => child.ChildSetupDraftId == otherParentDraft.Id);
    }

    [Fact]
    public async Task Handle_WhenClaimedDraftHasActiveRelationship_ReturnsActiveChildOnly()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        Student student = new(Guid.NewGuid());
        student.SetPrivateProperty(
            "User",
            new ApplicationUser { FirstName = "Linked", LastName = "Student" });

        ParentStudentRelationship activeRelationship =
            new(parent.Id, student.Id);
        activeRelationship.Accept();

        ChildSetupDraft claimedDraft = new(parent.Id);
        claimedDraft.SetName("Linked", "Student");
        claimedDraft.MarkInvited();
        claimedDraft.MarkClaimed(student.Id);

        GetParentOverviewChildrenQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(activeRelationship),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<ChildSetupDraft>(claimedDraft),
            new InMemoryRepository<ChildSetupInvitation>(),
            new FakeCurrentUser(parentUserId),
            new FixedTimeProvider(Now));

        IReadOnlyList<ParentOverviewChildResponse> result = await handler.Handle(
            new GetParentOverviewChildrenQuery(),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("ActiveChild", result[0].Type);
        Assert.Equal(student.Id, result[0].StudentId);
        Assert.Null(result[0].ChildSetupDraftId);
    }

    [Fact]
    public async Task Handle_WhenParentHasNoOverviewChildren_ReturnsEmptyList()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);

        GetParentOverviewChildrenQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(),
            new InMemoryRepository<Student>(),
            new InMemoryRepository<ChildSetupDraft>(),
            new InMemoryRepository<ChildSetupInvitation>(),
            new FakeCurrentUser(parentUserId),
            new FixedTimeProvider(Now));

        IReadOnlyList<ParentOverviewChildResponse> result = await handler.Handle(
            new GetParentOverviewChildrenQuery(),
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_ExcludesExpiredPendingInvitationsFromOverview()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);

        ChildSetupDraft validDraft = new(parent.Id);
        validDraft.SetName("Valid", "Invitation");
        validDraft.MarkInvited();

        ChildSetupInvitation validInvitation = new(
            validDraft.Id,
            "VALID@EXAMPLE.COM",
            "VALID_HASH",
            Now.AddMinutes(1));

        ChildSetupDraft expiredDraft = new(parent.Id);
        expiredDraft.SetName("Expired", "Invitation");
        expiredDraft.MarkInvited();

        ChildSetupInvitation expiredInvitation = new(
            expiredDraft.Id,
            "EXPIRED@EXAMPLE.COM",
            "EXPIRED_HASH",
            Now);

        GetParentOverviewChildrenQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(),
            new InMemoryRepository<Student>(),
            new InMemoryRepository<ChildSetupDraft>(validDraft, expiredDraft),
            new InMemoryRepository<ChildSetupInvitation>(validInvitation, expiredInvitation),
            new FakeCurrentUser(parentUserId),
            new FixedTimeProvider(Now));

        IReadOnlyList<ParentOverviewChildResponse> result = await handler.Handle(
            new GetParentOverviewChildrenQuery(),
            CancellationToken.None);

        Assert.Equal(2, result.Count);

        ParentOverviewChildResponse validChild =
            Assert.Single(result, child => child.ChildSetupDraftId == validDraft.Id);
        Assert.Equal(validInvitation.Id, validChild.ChildSetupInvitationId);
        Assert.Equal("Pending", validChild.ChildSetupInvitationStatus);
        Assert.Equal(validInvitation.ExpiresAtUtc, validChild.ChildSetupInvitationExpiresAtUtc);

        ParentOverviewChildResponse expiredChild =
            Assert.Single(result, child => child.ChildSetupDraftId == expiredDraft.Id);
        Assert.Null(expiredChild.ChildSetupInvitationId);
        Assert.Null(expiredChild.ChildSetupInvitationStatus);
        Assert.Null(expiredChild.ChildSetupInvitationExpiresAtUtc);
        Assert.Equal(ChildSetupInvitationStatus.Pending, expiredInvitation.Status);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
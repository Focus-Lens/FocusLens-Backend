using FocusLens.Application.ChildSetup;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;

using DomainStudyTimeGoalPeriod = FocusLens.Domain.Students.StudyTimeGoalPeriod;

namespace FocusLens.Application.UnitTests.ChildSetup;

public sealed class CreateChildSetupLinkInvitationCommandHandlerTests
{
    [Fact]
    public async Task CreateLink_WithEmptyDraft_DoesNotRequireParentSetupData()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = new(parent.Id);
        draft.SetProfileSetupMode(ChildSetupProfileMode.ChildManaged);
        InMemoryRepository<ChildSetupInvitation> invitations = new();

        CreateChildSetupLinkInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            invitations,
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FakeInvitationUrlBuilder(),
            new FakeChildSetupInvitationTokenProtector(),
            TimeProvider.System
        );

        Result<ChildSetupInvitationResponse> result = await handler.Handle(
            new CreateChildSetupLinkInvitationCommand(draft.Id),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(ChildSetupStatus.Invited, draft.Status);
        Assert.Null((await invitations.GetAllAsync()).Single().TargetEmailNormalized);
    }

    [Fact]
    public async Task CreateLink_WithCompleteDraftWithoutGoal_CreatesLinkWithoutEmail()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        ChildSetupDraft draft = CreateCompleteDraft(parent.Id);
        draft.SetGoal(StudentGoal.BuildARoutine);
        InMemoryRepository<ChildSetupInvitation> invitations = new();

        CreateChildSetupLinkInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            invitations,
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FakeInvitationUrlBuilder(),
            new FakeChildSetupInvitationTokenProtector(),
            TimeProvider.System
        );

        Result<ChildSetupInvitationResponse> result = await handler.Handle(
            new CreateChildSetupLinkInvitationCommand(draft.Id),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(ChildSetupStatus.Invited, draft.Status);
        Assert.Single(await invitations.GetAllAsync());
    }

    [Fact]
    public async Task CreateLink_WithCompleteDraft_CreatesLinkWithoutEmail()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        ChildSetupDraft draft = CreateCompleteDraft(parent.Id);
        InMemoryRepository<ChildSetupInvitation> invitations = new();

        CreateChildSetupLinkInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            invitations,
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FakeInvitationUrlBuilder(),
            new FakeChildSetupInvitationTokenProtector(),
            TimeProvider.System
        );

        Result<ChildSetupInvitationResponse> result = await handler.Handle(
            new CreateChildSetupLinkInvitationCommand(draft.Id),
            CancellationToken.None
        );

        ChildSetupInvitation invitation = (await invitations.GetAllAsync()).Single();

        Assert.True(result.IsSuccess);
        Assert.Equal(ChildSetupInvitationType.Link, invitation.Type);
        Assert.Null(invitation.TargetEmailNormalized);
        Assert.NotNull(invitation.ProtectedToken);
        Assert.Equal(ChildSetupStatus.Invited, draft.Status);
        Assert.Contains("/invitations/child-setup/", result.Value.InvitationUrl);
    }

    private static Parent CreateParent(Guid userId)
    {
        Parent parent = new(userId);
        parent.SetWeekStartsOn(DayOfWeek.Monday);
        return parent;
    }

    private static ChildSetupDraft CreateCompleteDraft(Guid parentId)
    {
        ChildSetupDraft draft = new(parentId);
        draft.SetName("Karim", "Mahmoud");
        draft.SetGrade(StudentGrade.Grade10);
        draft.ReplaceSubjects([ChildSetupSubject.Predefined(StudentSubjectType.Math)]);
        draft.SetGoal(StudentGoal.BuildARoutine);
        draft.SetStudyTimeGoal(
            StudyTimeGoal
                .Create(
                    DomainStudyTimeGoalPeriod.Daily,
                    60,
                    [DayOfWeek.Monday],
                    new DateOnly(2026, 9, 21)
                )
                .Value
        );
        draft.SetProfileSetupMode(ChildSetupProfileMode.ParentManaged);
        return draft;
    }
}

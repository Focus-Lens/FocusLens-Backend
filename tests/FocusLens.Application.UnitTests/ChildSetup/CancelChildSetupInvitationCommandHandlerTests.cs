using FocusLens.Application.ChildSetup;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Students;
using DomainStudyTimeGoalPeriod = FocusLens.Domain.Students.StudyTimeGoalPeriod;

namespace FocusLens.Application.UnitTests.ChildSetup;

public sealed class CancelChildSetupInvitationCommandHandlerTests
{
    [Fact]
    public async Task Cancel_WithPendingInvitation_CancelsInvitation()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = CreateInvitedDraft(parent.Id);
        ChildSetupInvitation invitation = CreatePendingInvitation(draft.Id);

        InMemoryRepository<ChildSetupInvitation> invitations = new(invitation);

        CancelChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            invitations,
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork()
        );

        Result<Success> result = await handler.Handle(
            new CancelChildSetupInvitationCommand(draft.Id),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(ChildSetupInvitationStatus.Cancelled, invitation.Status);
        Assert.Equal(ChildSetupStatus.Draft, draft.Status);
    }

    [Fact]
    public async Task Cancel_AllowsDraftToBeInvitedAgain()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = CreateInvitedDraft(parent.Id);
        ChildSetupInvitation invitation = CreatePendingInvitation(draft.Id);

        CancelChildSetupInvitationCommandHandler cancelHandler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork()
        );


        Result<Success> cancelResult = await cancelHandler.Handle(
            new CancelChildSetupInvitationCommand(draft.Id),
            CancellationToken.None
        );


        Assert.True(cancelResult.IsSuccess);
        Assert.Equal(ChildSetupStatus.Draft, draft.Status);
        Assert.Equal(ChildSetupInvitationStatus.Cancelled, invitation.Status);
    }

    [Fact]
    public async Task Cancel_AllowsParentToCreateAnotherInvitation()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        parent.SetPrivateProperty(
            "User",
            new ApplicationUser { FirstName = "Karim", LastName = "Mahmoud" });
        parent.SetWeekStartsOn(DayOfWeek.Monday);
        ChildSetupDraft draft = CreateCompleteDraft(parent.Id);
        draft.MarkInvited();

        ChildSetupInvitation cancelledInvitation = CreatePendingInvitation(draft.Id);

        InMemoryRepository<ChildSetupInvitation> invitations = new(cancelledInvitation);

        CancelChildSetupInvitationCommandHandler cancelHandler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            invitations,
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork()
        );

        Result<Success> cancelResult = await cancelHandler.Handle(
            new CancelChildSetupInvitationCommand(draft.Id),
            CancellationToken.None
        );

        Assert.True(cancelResult.IsSuccess);
        Assert.Equal(ChildSetupStatus.Draft, draft.Status);
        Assert.Equal(ChildSetupInvitationStatus.Cancelled, cancelledInvitation.Status);


        draft.SetProfileSetupMode(ChildSetupProfileMode.ChildManaged);

        CreateChildSetupInvitationCommandHandler createHandler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            invitations,
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FakeEmailSender(),
            new FakeInvitationUrlBuilder(),
            new FakeChildSetupInvitationTokenProtector(),
            TimeProvider.System
        );

        Result<ChildSetupInvitationResponse> createResult = await createHandler.Handle(
            new CreateChildSetupInvitationCommand(
                draft.Id,
                new CreateChildSetupInvitationRequest("child@example.com")
            ),
            CancellationToken.None
        );

        Assert.True(createResult.IsSuccess, createResult.TopError.ToString());
        Assert.Equal("Pending", createResult.Value.Status);
        Assert.Equal(ChildSetupStatus.Invited, draft.Status);
        Assert.Equal(2, (await invitations.GetAllAsync()).Count());
        Assert.Contains(
            await invitations.GetAllAsync(),
            item => item.Status == ChildSetupInvitationStatus.Cancelled
        );
        Assert.Contains(
            await invitations.GetAllAsync(),
            item => item.Status == ChildSetupInvitationStatus.Pending
        );
    }

    [Fact]
    public async Task Cancel_WithDraftOwnedByAnotherParent_ReturnsNotFound()
    {
        Parent owner = new(Guid.NewGuid());
        Parent currentParent = new(Guid.NewGuid());
        ChildSetupDraft draft = CreateInvitedDraft(owner.Id);
        ChildSetupInvitation invitation = CreatePendingInvitation(draft.Id);

        CancelChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(currentParent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new FakeCurrentUser(currentParent.UserId),
            new FakeUnitOfWork()
        );

        Result<Success> result = await handler.Handle(
            new CancelChildSetupInvitationCommand(draft.Id),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetup.NotFound", result.TopError.Code);
        Assert.Equal(ChildSetupInvitationStatus.Pending, invitation.Status);
    }

    [Fact]
    public async Task Cancel_WithDraftThatIsNotInvited_ReturnsConflict()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = new(parent.Id);

        CancelChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ChildSetupInvitation>(),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork()
        );

        Result<Success> result = await handler.Handle(
            new CancelChildSetupInvitationCommand(draft.Id),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetup.NotInvited", result.TopError.Code);
    }

    [Fact]
    public async Task Cancel_WithNoPendingInvitation_ReturnsNotFound()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = CreateInvitedDraft(parent.Id);

        ChildSetupInvitation cancelledInvitation = CreatePendingInvitation(draft.Id);

        cancelledInvitation.Cancel();

        CancelChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ChildSetupInvitation>(cancelledInvitation),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork()
        );

        Result<Success> result = await handler.Handle(
            new CancelChildSetupInvitationCommand(draft.Id),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetupInvitation.NotFound", result.TopError.Code);
    }

    [Fact]
    public async Task Cancel_WithEmptyDraftId_ReturnsValidationError()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);

        CancelChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(),
            new InMemoryRepository<ChildSetupInvitation>(),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork()
        );

        Result<Success> result = await handler.Handle(
            new CancelChildSetupInvitationCommand(Guid.Empty),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetup.InvalidDraftId", result.TopError.Code);
    }

    [Fact]
    public async Task Cancel_WithoutCurrentUser_ReturnsUnauthorized()
    {
        Parent parent = new(Guid.NewGuid());
        ChildSetupDraft draft = CreateInvitedDraft(parent.Id);
        ChildSetupInvitation invitation = CreatePendingInvitation(draft.Id);

        CancelChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new FakeCurrentUser(Guid.Empty),
            new FakeUnitOfWork()
        );

        Result<Success> result = await handler.Handle(
            new CancelChildSetupInvitationCommand(draft.Id),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal("Parents.CurrentUserUnavailable", result.TopError.Code);
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
                    new DateOnly(2026, 9, 14)
                )
                .Value
        );

        return draft;
    }

    private static ChildSetupDraft CreateInvitedDraft(Guid parentId)
    {
        ChildSetupDraft draft = new(parentId);
        draft.MarkInvited();
        return draft;
    }

    private static ChildSetupInvitation CreatePendingInvitation(Guid draftId)
    {
        return new ChildSetupInvitation(
            draftId,
            "child@example.com",
            "test-hash",
            DateTimeOffset.UtcNow.AddDays(7)
        );
    }
}
using System.Security.Cryptography;
using System.Text;
using FocusLens.Application.ChildSetup;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using DomainStudyPriority = FocusLens.Domain.Students.StudyPriority;
using DomainStudyTimeGoalPeriod = FocusLens.Domain.Students.StudyTimeGoalPeriod;

namespace FocusLens.Application.UnitTests.ChildSetup;

public sealed class CreateChildSetupInvitationCommandHandlerTests
{
    [Fact]
    public async Task Create_WithCompleteDraft_CreatesInvitationAndEmailsChild()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        ChildSetupDraft draft = CreateCompleteDraft(parent.Id);

        InMemoryRepository<ChildSetupInvitation> invitations = new();
        FakeEmailSender emailSender = new();
        CreateChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            invitations,
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            emailSender,
            new FakeInvitationUrlBuilder(),
            new FakeChildSetupInvitationTokenProtector(),
            TimeProvider.System);

        Result<ChildSetupInvitationResponse> result = await handler.Handle(
            new CreateChildSetupInvitationCommand(
                draft.Id,
                new CreateChildSetupInvitationRequest("child@example.com")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value.Status);
        Assert.Equal(ChildSetupStatus.Invited, draft.Status);
        Assert.Single(await invitations.GetAllAsync());
        Assert.Single(emailSender.ChildSetupInvitations);
        Assert.Equal("child@example.com", emailSender.ChildSetupInvitations[0].ChildEmail);
        Assert.Contains("/invitations/child-setup/", emailSender.ChildSetupInvitations[0].InvitationUrl);
        Assert.Equal(result.Value.Id, (await invitations.GetAllAsync()).Single().Id);
    }

    [Fact]
    public async Task Create_WithCompleteDraft_StoresHashOfGeneratedToken()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        ChildSetupDraft draft = CreateCompleteDraft(parent.Id);
        InMemoryRepository<ChildSetupInvitation> invitations = new();
        FakeEmailSender emailSender = new();

        CreateChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            invitations,
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            emailSender,
            new FakeInvitationUrlBuilder(),
            new FakeChildSetupInvitationTokenProtector(),
            TimeProvider.System);

        Result<ChildSetupInvitationResponse> result = await handler.Handle(
            new CreateChildSetupInvitationCommand(
                draft.Id,
                new CreateChildSetupInvitationRequest("child@example.com")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        ChildSetupInvitation invitation = (await invitations.GetAllAsync()).Single();
        string token = emailSender.ChildSetupInvitations.Single().InvitationUrl.Split((char)47).Last();

        Assert.Equal(
            Hash(token),
            invitation.TokenHash);
        Assert.NotEqual(token, invitation.TokenHash);
        Assert.Equal($"protected:{token}", invitation.ProtectedToken);
        Assert.NotEqual(token, invitation.ProtectedToken);
    }

    [Fact]
    public async Task Create_WithDraftOwnedByAnotherParent_ReturnsNotFound()
    {
        Parent owner = new(Guid.NewGuid());
        Parent currentParent = new(Guid.NewGuid());
        ChildSetupDraft draft = CreateCompleteDraft(owner.Id);

        CreateChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(currentParent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ChildSetupInvitation>(),
            new FakeCurrentUser(currentParent.UserId),
            new FakeUnitOfWork(),
            new FakeEmailSender(),
            new FakeInvitationUrlBuilder(),
            new FakeChildSetupInvitationTokenProtector(),
            TimeProvider.System);

        Result<ChildSetupInvitationResponse> result = await handler.Handle(
            new CreateChildSetupInvitationCommand(
                draft.Id,
                new CreateChildSetupInvitationRequest("child@example.com")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetup.NotFound", result.TopError.Code);
        Assert.Equal(ChildSetupStatus.Draft, draft.Status);
    }

    [Fact]
    public async Task Create_WithIncompleteDraft_ReturnsValidationError()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        ChildSetupDraft draft = new(parent.Id);

        CreateChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ChildSetupInvitation>(),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FakeEmailSender(),
            new FakeInvitationUrlBuilder(),
            new FakeChildSetupInvitationTokenProtector(),
            TimeProvider.System);

        Result<ChildSetupInvitationResponse> result = await handler.Handle(
            new CreateChildSetupInvitationCommand(
                draft.Id,
                new CreateChildSetupInvitationRequest("child@example.com")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetup.Incomplete", result.TopError.Code);
        Assert.Equal(ChildSetupStatus.Draft, draft.Status);
    }

    [Fact]
    public async Task Create_WithInvalidEmail_ReturnsValidationError()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        ChildSetupDraft draft = CreateCompleteDraft(parent.Id);

        CreateChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ChildSetupInvitation>(),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FakeEmailSender(),
            new FakeInvitationUrlBuilder(),
            new FakeChildSetupInvitationTokenProtector(),
            TimeProvider.System);

        Result<ChildSetupInvitationResponse> result = await handler.Handle(
            new CreateChildSetupInvitationCommand(
                draft.Id,
                new CreateChildSetupInvitationRequest("not-an-email")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetup.ChildEmailInvalid", result.TopError.Code);
    }

    [Fact]
    public async Task Create_WhenDraftAlreadyInvited_ReturnsConflict()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        ChildSetupDraft draft = CreateCompleteDraft(parent.Id);
        draft.MarkInvited();

        CreateChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<ChildSetupInvitation>(),
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            new FakeEmailSender(),
            new FakeInvitationUrlBuilder(),
            new FakeChildSetupInvitationTokenProtector(),
            TimeProvider.System);

        Result<ChildSetupInvitationResponse> result = await handler.Handle(
            new CreateChildSetupInvitationCommand(
                draft.Id,
                new CreateChildSetupInvitationRequest("child@example.com")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetup.DraftAlreadyInvited", result.TopError.Code);
    }

    [Fact]
    public async Task Create_WhenParentHasNotSelectedWeekStart_CreatesInvitation()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = CreateCompleteDraft(parent.Id);

        InMemoryRepository<ChildSetupInvitation> invitations = new();
        FakeEmailSender emailSender = new();

        CreateChildSetupInvitationCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            invitations,
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork(),
            emailSender,
            new FakeInvitationUrlBuilder(),
            new FakeChildSetupInvitationTokenProtector(),
            TimeProvider.System);

        Result<ChildSetupInvitationResponse> result = await handler.Handle(
            new CreateChildSetupInvitationCommand(
                draft.Id,
                new CreateChildSetupInvitationRequest("child@example.com")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value.Status);
        Assert.Equal(ChildSetupStatus.Invited, draft.Status);
        Assert.Single(await invitations.GetAllAsync());
        Assert.Single(emailSender.ChildSetupInvitations);
    }

    private static ChildSetupDraft CreateCompleteDraft(Guid parentId)
    {
        ChildSetupDraft draft = new(parentId);
        draft.SetName("Karim", "Mahmoud");
        draft.SetGrade(StudentGrade.Grade10);
        draft.ReplaceSubjects([
            ChildSetupSubject.Predefined(StudentSubjectType.Math)
        ]);
        draft.ReplaceStudyPriorities([
            DomainStudyPriority.BuildStudyRoutine,
            DomainStudyPriority.StayFocused,
            DomainStudyPriority.ExamPreparation
        ]);
        draft.SetStudyTimeGoal(
            StudyTimeGoal.Create(
                DomainStudyTimeGoalPeriod.Daily,
                60,
                [DayOfWeek.Monday],
                new DateOnly(2026, 9, 14)).Value);
        return draft;
    }

    private static string Hash(string token)
    {
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static Parent CreateParent(Guid userId)
    {
        Parent parent = new(userId);
        parent.SetWeekStartsOn(DayOfWeek.Monday);
        return parent;
    }
}

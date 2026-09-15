using System.Security.Cryptography;
using System.Text;
using FocusLens.Application.ChildSetup;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using StudentGrade = FocusLens.Contracts.Students.StudentGrade;
using StudentSubjectType = FocusLens.Domain.Students.StudentSubjectType;
using StudyPriority = FocusLens.Contracts.Students.StudyPriority;

namespace FocusLens.Application.UnitTests.ChildSetup;

public sealed class GetChildSetupInvitationQueryHandlerTests
{
    [Fact]
    public async Task Get_WithValidToken_ReturnsInvitationDetails()
    {
        const string token = "valid-child-setup-token";
        Parent parent = new(Guid.NewGuid());
        parent.SetWeekStartsOn(DayOfWeek.Saturday);
        ChildSetupDraft draft = CreateCompleteDraft(parent.Id);
        ChildSetupInvitation invitation = new(
            draft.Id,
            "CHILD@EXAMPLE.COM",
            Hash(token),
            DateTimeOffset.UtcNow.AddDays(1));

        GetChildSetupInvitationQueryHandler handler = new(
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<Parent>(parent),
            TimeProvider.System);

        Result<ChildSetupInvitationDetailsResponse> result = await handler.Handle(
            new GetChildSetupInvitationQuery(token),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value.Status);
        Assert.Equal("Karim", result.Value.FirstName);
        Assert.Equal("Mahmoud", result.Value.LastName);
        Assert.Equal(StudentGrade.Grade10, result.Value.Grade);
        Assert.Single(result.Value.Subjects);
        Assert.Equal("Math", result.Value.Subjects.Single().Type);
        Assert.Equal(StudyPriority.BuildStudyRoutine, result.Value.StudyPriorities.Single());
        Assert.Equal("Daily", result.Value.StudyTimeGoal!.Period);
        Assert.Equal(60, result.Value.StudyTimeGoal.TargetMinutes);
        Assert.Equal([DayOfWeek.Saturday, DayOfWeek.Monday], result.Value.StudyTimeGoal.Days);
    }

    [Fact]
    public async Task Get_WithUnknownToken_ReturnsNotFound()
    {
        ChildSetupDraft draft = CreateCompleteDraft(Guid.NewGuid());
        ChildSetupInvitation invitation = new(
            draft.Id,
            "CHILD@EXAMPLE.COM",
            Hash("different-token"),
            DateTimeOffset.UtcNow.AddDays(1));

        GetChildSetupInvitationQueryHandler handler = new(
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<Parent>(),
            TimeProvider.System);

        Result<ChildSetupInvitationDetailsResponse> result = await handler.Handle(
            new GetChildSetupInvitationQuery("unknown-token"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetupInvitation.NotFound", result.TopError.Code);
    }

    [Fact]
    public async Task Get_WithExpiredInvitation_ReturnsExpired()
    {
        const string token = "expired-token";
        ChildSetupDraft draft = CreateCompleteDraft(Guid.NewGuid());
        ChildSetupInvitation invitation = new(
            draft.Id,
            "CHILD@EXAMPLE.COM",
            Hash(token),
            DateTimeOffset.UtcNow.AddMinutes(-1));

        GetChildSetupInvitationQueryHandler handler = new(
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<Parent>(),
            TimeProvider.System);

        Result<ChildSetupInvitationDetailsResponse> result = await handler.Handle(
            new GetChildSetupInvitationQuery(token),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetupInvitation.Expired", result.TopError.Code);
    }

    [Fact]
    public async Task Get_WithClaimedInvitation_ReturnsUnavailable()
    {
        const string token = "claimed-token";
        ChildSetupDraft draft = CreateCompleteDraft(Guid.NewGuid());
        ChildSetupInvitation invitation = new(
            draft.Id,
            "CHILD@EXAMPLE.COM",
            Hash(token),
            DateTimeOffset.UtcNow.AddDays(1));
        invitation.Claim(DateTimeOffset.UtcNow);

        GetChildSetupInvitationQueryHandler handler = new(
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new InMemoryRepository<Parent>(),
            TimeProvider.System);

        Result<ChildSetupInvitationDetailsResponse> result = await handler.Handle(
            new GetChildSetupInvitationQuery(token),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetupInvitation.Unavailable", result.TopError.Code);
    }

    [Fact]
    public async Task Get_WhenDraftDoesNotExist_ReturnsNotFound()
    {
        const string token = "missing-draft-token";
        Guid draftId = Guid.NewGuid();
        ChildSetupInvitation invitation = new(
            draftId,
            "CHILD@EXAMPLE.COM",
            Hash(token),
            DateTimeOffset.UtcNow.AddDays(1));

        GetChildSetupInvitationQueryHandler handler = new(
            new InMemoryRepository<ChildSetupInvitation>(invitation),
            new InMemoryRepository<ChildSetupDraft>(),
            new InMemoryRepository<Parent>(),
            TimeProvider.System);

        Result<ChildSetupInvitationDetailsResponse> result = await handler.Handle(
            new GetChildSetupInvitationQuery(token),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ChildSetupInvitation.NotFound", result.TopError.Code);
    }

    private static ChildSetupDraft CreateCompleteDraft(Guid parentId)
    {
        ChildSetupDraft draft = new(parentId);
        draft.SetName("Karim", "Mahmoud");
        draft.SetGrade(Domain.Students.StudentGrade.Grade10);
        draft.ReplaceSubjects([
            ChildSetupSubject.Predefined(StudentSubjectType.Math)
        ]);
        draft.ReplaceStudyPriorities([
            Domain.Students.StudyPriority.BuildStudyRoutine
        ]);
        draft.SetStudyTimeGoal(
            StudyTimeGoal.Create(
                StudyTimeGoalPeriod.Daily,
                60,
                [DayOfWeek.Monday, DayOfWeek.Saturday],
                new DateOnly(2026, 9, 14)).Value);
        return draft;
    }

    private static string Hash(string token)
    {
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}

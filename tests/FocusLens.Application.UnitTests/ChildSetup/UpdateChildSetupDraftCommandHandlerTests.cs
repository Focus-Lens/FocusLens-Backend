using FocusLens.Application.ChildSetup;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using DomainStudyTimeGoalPeriod = FocusLens.Domain.Students.StudyTimeGoalPeriod;

namespace FocusLens.Application.UnitTests.ChildSetup;

public sealed class UpdateChildSetupDraftCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithTargetHours_CreatesWeeklyGoalFromParentWeekStart()
    {
        Parent parent = new(Guid.NewGuid());
        parent.SetWeekStartsOn(DayOfWeek.Saturday);
        ChildSetupDraft draft = new(parent.Id);

        Result<ChildSetupDraftResponse> result = await CreateHandler(parent, draft).Handle(
            new UpdateChildSetupDraftCommand(draft.Id, ValidRequest(new ChildSetupStudyTimeGoalRequest(5))),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(draft.StudyTimeGoal);
        Assert.Equal(DomainStudyTimeGoalPeriod.Weekly, draft.StudyTimeGoal!.Period);
        Assert.Equal(300, draft.StudyTimeGoal.TargetMinutes);
        Assert.Equal([DayOfWeek.Saturday], draft.StudyTimeGoal.Days);
        Assert.Equal(new DateOnly(2026, 9, 12), draft.StudyTimeGoal.StartDate);
    }

    [Fact]
    public async Task Handle_WithTargetHoursAndNoParentWeekStart_ReturnsValidationError()
    {
        Parent parent = new(Guid.NewGuid());
        ChildSetupDraft draft = new(parent.Id);

        Result<ChildSetupDraftResponse> result = await CreateHandler(parent, draft).Handle(
            new UpdateChildSetupDraftCommand(draft.Id, ValidRequest(new ChildSetupStudyTimeGoalRequest(5))),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("ChildSetup.WeekStartsOnRequired", result.TopError.Code);
        Assert.Null(draft.StudyTimeGoal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(999999999)]
    public async Task Handle_WithInvalidTargetHours_ReturnsValidationError(decimal targetHours)
    {
        Parent parent = new(Guid.NewGuid());
        parent.SetWeekStartsOn(DayOfWeek.Monday);
        ChildSetupDraft draft = new(parent.Id);

        Result<ChildSetupDraftResponse> result = await CreateHandler(parent, draft).Handle(
            new UpdateChildSetupDraftCommand(
                draft.Id,
                ValidRequest(new ChildSetupStudyTimeGoalRequest(targetHours))),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("ChildSetup.InvalidStudyTimeGoal", result.TopError.Code);
        Assert.Null(draft.StudyTimeGoal);
    }

    private static UpdateChildSetupDraftRequest ValidRequest(
        ChildSetupStudyTimeGoalRequest? studyTimeGoal) =>
        new(
            "Dina",
            "Student",
            new DateOnly(2010, 5, 12),
            StudentGrade.Grade10,
            null,
            [new StudentSubjectRequest(StudentSubjectType.Math, null)],
            StudentGoal.FocusBetter,
            studyTimeGoal);

    private static UpdateChildSetupDraftCommandHandler CreateHandler(
        Parent parent,
        ChildSetupDraft draft) =>
        new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            new FakeCurrentUser(parent.UserId),
            new FakeUnitOfWork(),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero)));

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

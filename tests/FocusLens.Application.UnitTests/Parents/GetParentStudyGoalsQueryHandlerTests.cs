using FocusLens.Application.Parents;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using FocusLens.Domain.StudySessions;

namespace FocusLens.Application.UnitTests.Parents;

public sealed class GetParentStudyGoalsQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WithActiveGoal_DoesNotExposeUnrelatedPendingProposal()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        parent.SetWeekStartsOn(DayOfWeek.Saturday);
        Student student = new(Guid.NewGuid());
        student.SetWeekStartsOn(DayOfWeek.Saturday);
        student.SetStudyTimeGoal(StudyTimeGoal.Create(
            StudyTimeGoalPeriod.Weekly, 90, [DayOfWeek.Saturday], new DateOnly(2026, 9, 19)).Value);
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        StudyGoalProposal stalePending = new(parent.Id, student.Id, StudyTimeGoal.Create(
            StudyTimeGoalPeriod.Weekly, 120, [DayOfWeek.Saturday], new DateOnly(2026, 9, 12)).Value);

        GetParentStudyGoalsQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudyGoalProposal>(stalePending),
            new InMemoryRepository<StudySession>(),
            new InMemoryRepository<StudentWeek>(new StudentWeek(student.Id, new DateOnly(2026, 9, 19))),
            new FakeCurrentUser(parentUserId),
            new FixedTimeProvider(Now));

        Result<ParentStudyGoalsResponse> result = await handler.Handle(
            new GetParentStudyGoalsQuery(student.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.CurrentStudyGoal);
        Assert.NotNull(result.Value.CurrentStudyGoalProgress);
        Assert.Null(result.Value.PendingStudyGoalProposal);
        Assert.Null(result.Value.CurrentStudyGoalAcceptedProposal);
        ParentStudyGoalWeekResponse thisWeek = Assert.IsType<ParentStudyGoalWeekResponse>(result.Value.ThisWeek);
        Assert.Equal(7, thisWeek.Days.Count);
        Assert.Equal(
            [
                new DateOnly(2026, 9, 19),
                new DateOnly(2026, 9, 20),
                new DateOnly(2026, 9, 21),
                new DateOnly(2026, 9, 22),
                new DateOnly(2026, 9, 23),
                new DateOnly(2026, 9, 24),
                new DateOnly(2026, 9, 25)
            ],
            thisWeek.Days.Select(day => day.Date));
        Assert.Equal(
            ["Saturday", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
            thisWeek.Days.Select(day => day.Day));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
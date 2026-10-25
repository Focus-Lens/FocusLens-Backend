using FocusLens.Application.Common.Services;
using FocusLens.Application.Home;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Domain;
using FocusLens.Domain.Students;
using FocusLens.Domain.StudySessions;

namespace FocusLens.Application.UnitTests.Home;

public sealed class HomeQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Overview_UsesCompletedActualDurationAndExcludesPausedTime()
    {
        Student student = Student();
        DateTimeOffset started = new(2026, 9, 20, 8, 0, 0, TimeSpan.Zero);
        StudySession completed = ReadySession(student.Id, 120);
        Assert.True(completed.Start(started).IsSuccess);
        Assert.True(completed.Pause(started.AddMinutes(20)).IsSuccess);
        Assert.True(completed.Resume(started.AddMinutes(35)).IsSuccess);
        Assert.True(completed.CompleteSuccessfully(started.AddMinutes(65)).IsSuccess);

        var result = await Overview(student, completed);

        Assert.Equal(50, result!.FocusTimeMinutes);
        Assert.True(result.HasStartedSession);
    }

    [Fact]
    public async Task Overview_ExcludesActiveCancelledAndNotStartedSessionsFromFocusTime()
    {
        Student student = Student();
        DateTimeOffset started = new(2026, 9, 20, 8, 0, 0, TimeSpan.Zero);
        StudySession active = ReadySession(student.Id, 90);
        Assert.True(active.Start(started).IsSuccess);
        Assert.True(active.UpdateProgress(1, started.AddMinutes(30)).IsSuccess);
        StudySession cancelled = ReadySession(student.Id, 90);
        Assert.True(cancelled.Start(started.AddMinutes(40)).IsSuccess);
        Assert.True(cancelled.End(started.AddMinutes(70)).IsSuccess);
        StudySession notStarted = ReadySession(student.Id, 90);

        var result = await Overview(student, active, cancelled, notStarted);

        Assert.Equal(0, result!.FocusTimeMinutes);
        Assert.Equal(2, result.SessionsToday);
        Assert.Equal(1, result.StreakDays);
        Assert.True(result.HasStartedSession);
    }

    [Fact]
    public async Task Overview_UsesStudentLocalWeekBoundaryAndSplitsCrossMidnightSession()
    {
        Student student = Student();
        student.SetTimeZoneId("Africa/Cairo");
        DateTimeOffset started = new(2026, 9, 19, 20, 30, 0, TimeSpan.Zero); // 23:30 local Saturday
        StudySession completed = Completed(student.Id, started, 60);
        StudySession previousWeek = Completed(student.Id, started.AddDays(-2), 30);

        var result = await Overview(student, completed, previousWeek);

        Assert.Equal(60, result!.FocusTimeMinutes);
    }

    [Fact]
    public async Task Overview_ReturnsActiveWeeklyGoal()
    {
        Student student = Student();
        student.SetStudyTimeGoal(Goal(StudyTimeGoalPeriod.Weekly, 150, new DateOnly(2026, 9, 19)));
        StudySession completed = Completed(student.Id, new DateTimeOffset(2026, 9, 20, 8, 0, 0, TimeSpan.Zero), 90);

        var result = await Overview(student, completed);

        Assert.Equal("2h 30m", result!.WeeklyGoalTime);
        Assert.Equal("1h 30m", result.WeeklyStudiedTime);
        Assert.Equal(60m, result.WeeklyGoalPercentage);
    }

    [Fact]
    public async Task Overview_ReturnsNullForDailyMissingOrExpiredWeeklyGoal()
    {
        Student daily = Student();
        daily.SetStudyTimeGoal(Goal(StudyTimeGoalPeriod.Daily, 180, new DateOnly(2026, 9, 20)));
        Student missing = Student();
        Student expired = Student();
        expired.SetStudyTimeGoal(Goal(StudyTimeGoalPeriod.Weekly, 180, new DateOnly(2026, 9, 13)));

        foreach (Student student in new[] { daily, missing, expired })
        {
            var result = await Overview(student);
            Assert.Null(result!.WeeklyGoalTime);
            Assert.Equal("0m", result.WeeklyStudiedTime);
            Assert.Equal(0m, result.WeeklyGoalPercentage);
        }
    }

    [Fact]
    public async Task Overview_OnlyTreatsStartedSessionsAsReturningUserHistory()
    {
        Student student = Student();
        StudySession draft = StudySession.Create(student.Id, StudySessionMode.Digital).Value;

        var result = await Overview(student, draft);

        Assert.False(result!.HasStartedSession);
        Assert.Equal(0, result.SessionsToday);
        Assert.Equal(0, result.StreakDays);
    }

    private static async Task<FocusLens.Contracts.Home.StudyOverviewResponse?> Overview(
        Student student, params StudySession[] sessions)
    {
        TimeProvider timeProvider = new FixedTimeProvider(Now);
        HomeQueryHandler handler = new(
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudySession>(sessions),
            new FakeCurrentUser(student.UserId),
            new StudentLocalTime(timeProvider),
            timeProvider);
        return await handler.Handle(new GetStudyOverviewQuery(), CancellationToken.None);
    }

    private static Student Student()
    {
        Student student = new(Guid.NewGuid());
        student.SetWeekStartsOn(DayOfWeek.Saturday);
        return student;
    }

    private static StudyTimeGoal Goal(StudyTimeGoalPeriod period, int targetMinutes, DateOnly startsOn) =>
        StudyTimeGoal.Create(period, targetMinutes,
            period == StudyTimeGoalPeriod.Weekly ? [DayOfWeek.Saturday] : [DayOfWeek.Sunday], startsOn).Value;

    private static StudySession Completed(Guid studentId, DateTimeOffset started, int minutes)
    {
        StudySession session = ReadySession(studentId, minutes);
        Assert.True(session.Start(started).IsSuccess);
        Assert.True(session.CompleteSuccessfully(started.AddMinutes(minutes)).IsSuccess);
        return session;
    }

    private static StudySession ReadySession(Guid studentId, int minutes)
    {
        StudySession session = StudySession.Create(studentId, StudySessionMode.Digital).Value;
        StudyMaterial material = StudyMaterial.Create(studentId, "book.pdf", 1, 1,
            $"materials/{Guid.NewGuid()}/book.pdf", StudyMaterialSource.Upload).Value;
        Assert.True(session.SetSubjectId(Guid.NewGuid()).IsSuccess);
        Assert.True(session.SetDuration(minutes).IsSuccess);
        Assert.True(session.SetStudyMaterial(material).IsSuccess);
        Assert.True(session.SetSelection(StudySessionSelection.Create(session, material, 1, 1).Value).IsSuccess);
        Assert.True(session.SetSelectedSections([]).IsSuccess);
        Assert.True(session.MarkReady().IsSuccess);
        return session;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

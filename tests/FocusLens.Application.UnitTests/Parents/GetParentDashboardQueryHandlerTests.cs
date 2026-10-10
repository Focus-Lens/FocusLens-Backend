using FocusLens.Application.Parents;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using FocusLens.Domain.StudySessions;

namespace FocusLens.Application.UnitTests.Parents;

public sealed class GetParentDashboardQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Dashboard_UsesCurrentPersistedWeek_AndReportsAllAndCompletedSessionCounts()
    {
        TestContext context = CreateContext();
        StudentWeek currentWeek = new(context.Student.Id, new DateOnly(2026, 9, 19));
        StudySession completed =
            Completed(context.Student.Id, new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero), 30);
        StudySession active = Active(context.Student.Id, new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero), 45);
        GetParentDashboardQueryHandler handler = DashboardHandler(context, [currentWeek], [completed, active]);

        Result<ParentDashboardResponse> result =
            await handler.Handle(new GetParentDashboardQuery(context.Student.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(currentWeek.StartsOn, result.Value.Week.StartsOn);
        Assert.Equal(currentWeek.EndsOn, result.Value.Week.EndsOn);
        Assert.True(result.Value.Week.IsCurrentWeek);
        Assert.Equal(2, result.Value.WeeklyStudyPulse.TotalSessions);
        Assert.Equal(1, result.Value.WeeklyStudyPulse.CompletedSessions);
        Assert.Equal(2, result.Value.WeeklyStudyPulse.ActiveStudyDays);
        Assert.Equal("1 hour 15 minutes", result.Value.WeeklyStudyPulse.TotalStudyMinutes);
        Assert.Equal("0 minutes", result.Value.WeeklyStudyPulse.PreviousWeekStudyMinutes);
        Assert.Equal("1 hour 15 minutes", result.Value.WeeklyStudyPulse.StudyMinutesTrend);
        Assert.Equal("45 minutes", result.Value.WeeklyStudyPulse.PeakDay!.StudyMinutes);
        Assert.Contains(result.Value.WeeklyStudyPulse.Days, day => day.StudyMinutes == "30 minutes");
        Assert.Contains(result.Value.FocusPattern.Days.SelectMany(day => day.Sessions),
            session => session.StudyMinutes == "45 minutes");
        Assert.Null(typeof(ParentDashboardResponse).GetProperty("RecentStudySessions"));
        Assert.Null(typeof(ParentDashboardWeeklyStudyPulseResponse).GetProperty("StudySessionsCount"));
    }

    [Fact]
    public async Task DashboardAndSessions_UseTheSameSelectedHistoricalStudentWeek()
    {
        TestContext context = CreateContext();
        StudentWeek historical = new(context.Student.Id, new DateOnly(2026, 9, 12));
        StudentWeek current = new(context.Student.Id, new DateOnly(2026, 9, 19));
        context.Relationship.SetSelectedOverviewWeekStart(historical.StartsOn);
        StudySession oldSession =
            Completed(context.Student.Id, new DateTimeOffset(2026, 9, 12, 9, 0, 0, TimeSpan.Zero), 40);
        StudySession currentSession =
            Completed(context.Student.Id, new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero), 30);
        InMemoryRepository<StudentWeek> weeks = new(historical, current);

        Result<ParentDashboardResponse> dashboard = await DashboardHandler(context, weeks, [oldSession, currentSession])
            .Handle(new GetParentDashboardQuery(context.Student.Id), CancellationToken.None);
        Result<ParentDashboardSessionHistoryResponse> sessions =
            await SessionsHandler(context, weeks, [oldSession, currentSession])
                .Handle(new GetParentDashboardSessionsQuery(context.Student.Id, null, null), CancellationToken.None);

        Assert.True(dashboard.IsSuccess);
        Assert.Equal(historical.StartsOn, dashboard.Value.Week.StartsOn);
        Assert.Equal("40 minutes", dashboard.Value.WeeklyStudyPulse.TotalStudyMinutes);
        Assert.True(sessions.IsSuccess);
        Assert.Equal(historical.StartsOn, sessions.Value.Week!.StartsOn);
        Assert.Single(sessions.Value.Sessions);
        Assert.Equal(oldSession.Id, sessions.Value.Sessions.Single().Id);
        Assert.Equal("40 minutes", sessions.Value.Sessions.Single().ActualStudyMinutes);
        Assert.Equal("40 minutes", sessions.Value.Sessions.Single().PlannedFocusDurationMinutes);
    }

    [Fact]
    public async Task Dashboard_WorksForCurrentWeekWithoutGoalOrSessions()
    {
        TestContext context = CreateContext();
        StudentWeek current = new(context.Student.Id, new DateOnly(2026, 9, 19));

        Result<ParentDashboardResponse> result = await DashboardHandler(context, [current], [])
            .Handle(new GetParentDashboardQuery(context.Student.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.WeeklyStudyPulse.TotalSessions);
        Assert.Equal(7, result.Value.FocusPattern.Days.Count);
        Assert.All(result.Value.FocusPattern.Days, day => Assert.Empty(day.Sessions));
    }

    [Fact]
    public async Task Dashboard_AttributesCrossMidnightStudyToEachStudentLocalDay()
    {
        TestContext context = CreateContext();
        context.Student.SetTimeZoneId("Africa/Cairo");
        StudentWeek currentWeek = new(context.Student.Id, new DateOnly(2026, 9, 19));
        // 20:30-21:30 UTC is 23:30-00:30 in Cairo in September.
        StudySession session = Completed(context.Student.Id,
            new DateTimeOffset(2026, 9, 19, 20, 30, 0, TimeSpan.Zero), 60);

        Result<ParentDashboardResponse> result = await DashboardHandler(context, [currentWeek], [session])
            .Handle(new GetParentDashboardQuery(context.Student.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("1 hour", result.Value.WeeklyStudyPulse.TotalStudyMinutes);
        Assert.Contains(result.Value.WeeklyStudyPulse.Days,
            day => day.Date == new DateOnly(2026, 9, 19) && day.StudyMinutes == "30 minutes");
        Assert.Contains(result.Value.WeeklyStudyPulse.Days,
            day => day.Date == new DateOnly(2026, 9, 20) && day.StudyMinutes == "30 minutes");
    }

    private static GetParentDashboardQueryHandler DashboardHandler(TestContext context, IEnumerable<StudentWeek> weeks,
        IEnumerable<StudySession> sessions) =>
        DashboardHandler(context, new InMemoryRepository<StudentWeek>(weeks.ToArray()), sessions);

    private static GetParentDashboardQueryHandler DashboardHandler(TestContext context,
        InMemoryRepository<StudentWeek> weeks, IEnumerable<StudySession> sessions) => new(
        new InMemoryRepository<Parent>(context.Parent),
        new InMemoryRepository<ParentStudentRelationship>(context.Relationship),
        new InMemoryRepository<Student>(context.Student), new InMemoryRepository<StudyGoalProposal>(),
        new InMemoryRepository<StudySession>(sessions.ToArray()), new InMemoryRepository<StudySessionBehaviorWindow>(),
        weeks,
        new FakeCurrentUser(context.Parent.UserId), new FixedTimeProvider(Now));

    private static GetParentDashboardSessionsQueryHandler SessionsHandler(TestContext context,
        InMemoryRepository<StudentWeek> weeks, IEnumerable<StudySession> sessions) => new(
        new InMemoryRepository<Parent>(context.Parent),
        new InMemoryRepository<ParentStudentRelationship>(context.Relationship),
        new InMemoryRepository<Student>(context.Student), new InMemoryRepository<StudySession>(sessions.ToArray()),
        new InMemoryRepository<StudySessionBehaviorWindow>(), weeks,
        new FakeCurrentUser(context.Parent.UserId), new FixedTimeProvider(Now));

    private static TestContext CreateContext()
    {
        Parent parent = new(Guid.NewGuid());
        parent.SetWeekStartsOn(DayOfWeek.Saturday);
        Student student = new(Guid.NewGuid());
        student.SetWeekStartsOn(DayOfWeek.Saturday);
        student.SetParentSharingPreferences(true, true);
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        return new TestContext(parent, student, relationship);
    }

    private static StudySession Completed(Guid studentId, DateTimeOffset started, int minutes)
    {
        StudySession session = ReadySession(studentId, minutes);
        Assert.True(session.Start(started).IsSuccess);
        Assert.True(session.CompleteSuccessfully(started.AddMinutes(minutes)).IsSuccess);
        return session;
    }

    private static StudySession Active(Guid studentId, DateTimeOffset started, int minutes)
    {
        StudySession session = ReadySession(studentId, minutes);
        Assert.True(session.Start(started).IsSuccess);
        Assert.True(session.UpdateProgress(1, started.AddMinutes(minutes)).IsSuccess);
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

    private sealed record TestContext(Parent Parent, Student Student, ParentStudentRelationship Relationship);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
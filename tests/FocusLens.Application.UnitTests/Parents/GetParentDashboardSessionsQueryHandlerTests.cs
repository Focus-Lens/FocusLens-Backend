using FocusLens.Application.Parents;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using FocusLens.Domain.StudySessions;

namespace FocusLens.Application.UnitTests.Parents;

public sealed class GetParentDashboardSessionsQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Sessions_UsesLatestBehaviorWindowForFocusAnalysis()
    {
        TestContext context = CreateContext();
        StudySession session = Completed(context.Student.Id, new DateTimeOffset(2026, 9, 20, 9, 0, 0, TimeSpan.Zero), 30);
        StudySessionBehaviorWindow first = BehaviorWindow(session.Id, 1, 42, "DISTRACTED", "DECLINING");
        StudySessionBehaviorWindow latest = BehaviorWindow(session.Id, 2, 87, "DEEP_FOCUS", "IMPROVING");

        Result<ParentDashboardSessionHistoryResponse> result = await CreateHandler(context, [session], [first, latest])
            .Handle(new(context.Student.Id, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        ParentDashboardStudySessionResponse response = Assert.Single(result.Value.Sessions);
        Assert.Equal(87, response.FocusQuality);
        Assert.Equal("DEEP_FOCUS", response.FocusState);
        Assert.Equal("IMPROVING", response.FocusTrend);
    }

    [Fact]
    public async Task Sessions_WithoutBehaviorWindowReturnsNullFocusAnalysis()
    {
        TestContext context = CreateContext();
        StudySession session = Completed(context.Student.Id, new DateTimeOffset(2026, 9, 20, 9, 0, 0, TimeSpan.Zero), 30);

        Result<ParentDashboardSessionHistoryResponse> result = await CreateHandler(context, [session])
            .Handle(new(context.Student.Id, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        ParentDashboardStudySessionResponse response = Assert.Single(result.Value.Sessions);
        Assert.Null(response.FocusQuality);
        Assert.Null(response.FocusState);
        Assert.Null(response.FocusTrend);
    }

    [Fact]
    public async Task Sessions_PreservesExistingSubjectStatusFilteringAndPagination()
    {
        TestContext context = CreateContext();
        Guid includedSubjectId = Guid.NewGuid();
        StudySession mostRecent = Completed(context.Student.Id, new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero), 30, includedSubjectId);
        StudySession expectedPageSession = Completed(context.Student.Id, new DateTimeOffset(2026, 9, 20, 9, 0, 0, TimeSpan.Zero), 30, includedSubjectId);
        StudySession otherSubject = Completed(context.Student.Id, new DateTimeOffset(2026, 9, 20, 8, 0, 0, TimeSpan.Zero), 30, Guid.NewGuid());
        StudySession active = Active(context.Student.Id, new DateTimeOffset(2026, 9, 20, 7, 0, 0, TimeSpan.Zero), 30, includedSubjectId);

        Result<ParentDashboardSessionHistoryResponse> result = await CreateHandler(
                context,
                [mostRecent, expectedPageSession, otherSubject, active])
            .Handle(new(context.Student.Id, [nameof(StudySessionStatus.Completed)], includedSubjectId, 2, 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Pagination.TotalCount);
        Assert.Equal(2, result.Value.Pagination.TotalPages);
        Assert.Equal(2, result.Value.Pagination.Page);
        Assert.Equal(1, result.Value.Pagination.PageSize);
        Assert.Equal(expectedPageSession.Id, Assert.Single(result.Value.Sessions).Id);
    }

    [Fact]
    public async Task Sessions_RemainsForbiddenWhenSessionSummariesArePrivate()
    {
        TestContext context = CreateContext();
        context.Student.SetParentSharingPreferences(false, true);

        Result<ParentDashboardSessionHistoryResponse> result = await CreateHandler(context, [])
            .Handle(new(context.Student.Id, null, null), CancellationToken.None);

        Error error = Assert.Single(result.Errors);
        Assert.Equal("ParentDashboard.SessionSummariesPrivate", error.Code);
    }

    private static GetParentDashboardSessionsQueryHandler CreateHandler(
        TestContext context,
        IEnumerable<StudySession> sessions,
        IEnumerable<StudySessionBehaviorWindow>? behaviorWindows = null) => new(
        new InMemoryRepository<Parent>(context.Parent),
        new InMemoryRepository<ParentStudentRelationship>(context.Relationship),
        new InMemoryRepository<Student>(context.Student),
        new InMemoryRepository<StudySession>(sessions.ToArray()),
        new InMemoryRepository<StudySessionBehaviorWindow>((behaviorWindows ?? []).ToArray()),
        new InMemoryRepository<StudentWeek>(new StudentWeek(context.Student.Id, new DateOnly(2026, 9, 19))),
        new FakeCurrentUser(context.Parent.UserId),
        new FixedTimeProvider(Now));

    private static TestContext CreateContext()
    {
        Parent parent = new(Guid.NewGuid());
        parent.SetWeekStartsOn(DayOfWeek.Saturday);
        Student student = new(Guid.NewGuid());
        student.SetWeekStartsOn(DayOfWeek.Saturday);
        student.SetParentSharingPreferences(true, true);
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        return new(parent, student, relationship);
    }

    private static StudySessionBehaviorWindow BehaviorWindow(
        Guid sessionId,
        int windowIndex,
        int focusScore,
        string focusState,
        string focusTrend)
    {
        DateTimeOffset start = new(2026, 9, 20, 9, 0, 0, TimeSpan.Zero);
        StudySessionBehaviorWindow window = StudySessionBehaviorWindow.Create(
            sessionId, windowIndex, start, start.AddMinutes(5), false).Value;
        Assert.True(window.RecordAnalysis(focusScore, focusState, focusTrend, null, null, null, null, false).IsSuccess);
        return window;
    }

    private static StudySession Completed(Guid studentId, DateTimeOffset started, int minutes, Guid? subjectId = null)
    {
        StudySession session = ReadySession(studentId, minutes, subjectId);
        Assert.True(session.Start(started).IsSuccess);
        Assert.True(session.CompleteSuccessfully(started.AddMinutes(minutes)).IsSuccess);
        return session;
    }

    private static StudySession Active(Guid studentId, DateTimeOffset started, int minutes, Guid? subjectId = null)
    {
        StudySession session = ReadySession(studentId, minutes, subjectId);
        Assert.True(session.Start(started).IsSuccess);
        Assert.True(session.UpdateProgress(1, started.AddMinutes(minutes)).IsSuccess);
        return session;
    }

    private static StudySession ReadySession(Guid studentId, int minutes, Guid? subjectId)
    {
        StudySession session = StudySession.Create(studentId, StudySessionMode.Digital).Value;
        StudyMaterial material = StudyMaterial.Create(studentId, "book.pdf", 1, 1,
            $"materials/{Guid.NewGuid()}/book.pdf", StudyMaterialSource.Upload).Value;
        Assert.True(session.SetSubjectId(subjectId ?? Guid.NewGuid()).IsSuccess);
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

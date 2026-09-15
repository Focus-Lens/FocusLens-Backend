using System.Text;
using FocusLens.Application.Parents;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Contracts;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Students;
using FocusLens.Domain.StudySessions;

namespace FocusLens.Application.UnitTests.Parents;

public sealed class GetParentDashboardQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenRelationshipIsActive_ReturnsGoalPendingProposalRecentSessionsAndWeeklyPulse()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        Student student = new(Guid.NewGuid());
        student.SetParentSharingPreferences(true, true);
        StudentSubject math = StudentSubject.Predefined(StudentSubjectType.Math);
        student.Subjects.Add(math);

        Result<StudyTimeGoal> currentGoal = StudyTimeGoal.Create(
            StudyTimeGoalPeriod.Daily,
            30,
            [DayOfWeek.Monday],
            new DateOnly(2026, 9, 14)
        );
        student.SetStudyTimeGoal(currentGoal.Value);

        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();

        StudyGoalProposal pendingProposal = new(
            parent.Id,
            student.Id,
            StudyTimeGoal
                .Create(
                    StudyTimeGoalPeriod.Weekly,
                    120,
                    [DayOfWeek.Monday],
                    new DateOnly(2026, 9, 21)
                )
                .Value
        );

        StudySession completedSession = CreateCompletedSession(
            student.Id,
            math.Id,
            new DateTimeOffset(2026, 9, 19, 10, 0, 0, TimeSpan.Zero),
            30
        );
        StudySession pausedCompletedSession = CreatePausedCompletedSession(
            student.Id,
            math.Id,
            new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.Zero),
            30
        );
        StudySession activeSessionWithUnknownSubject = CreateActiveSession(
            student.Id,
            Guid.NewGuid(),
            new DateTimeOffset(2026, 9, 20, 11, 50, 0, TimeSpan.Zero),
            45
        );
        StudySession previousPeriodSession = CreateCompletedSession(
            student.Id,
            math.Id,
            new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero),
            15
        );
        StudySession otherStudentSession = CreateCompletedSession(
            Guid.NewGuid(),
            math.Id,
            new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero),
            60
        );

        GetParentDashboardQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudyGoalProposal>(pendingProposal),
            new InMemoryRepository<StudySession>(
                completedSession,
                pausedCompletedSession,
                activeSessionWithUnknownSubject,
                previousPeriodSession,
                otherStudentSession
            ),
            new InMemoryRepository<StudySessionBehaviorWindow>(),
            new FakeCurrentUser(parentUserId),
            new FixedTimeProvider(Now)
        );

        Result<ParentDashboardResponse> result = await handler.Handle(
            new GetParentDashboardQuery(student.Id),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(student.Id, result.Value.StudentId);
        Assert.NotNull(result.Value.CurrentStudyGoal);
        Assert.Equal("Daily", result.Value.CurrentStudyGoal.Period);
        Assert.Equal(30, result.Value.CurrentStudyGoal.TargetMinutes);
        Assert.NotNull(result.Value.PendingStudyGoalProposal);
        Assert.Equal(pendingProposal.Id, result.Value.PendingStudyGoalProposal.Id);
        Assert.Equal("Pending", result.Value.PendingStudyGoalProposal.Status);

        Assert.Equal(4, result.Value.RecentStudySessions.Count);
        Assert.Equal(
            [
                activeSessionWithUnknownSubject.Id,
                completedSession.Id,
                pausedCompletedSession.Id,
                previousPeriodSession.Id,
            ],
            result.Value.RecentStudySessions.Select(session => session.Id)
        );

        ParentDashboardStudySessionResponse completedResponse =
            result.Value.RecentStudySessions.Single(session => session.Id == completedSession.Id);
        Assert.Equal("Math", completedResponse.SubjectName);
        Assert.Equal(30, completedResponse.ActualStudyMinutes);
        Assert.Equal(30, completedResponse.PlannedFocusDurationMinutes);

        ParentDashboardStudySessionResponse unknownSubjectResponse =
            result.Value.RecentStudySessions.Single(session =>
                session.Id == activeSessionWithUnknownSubject.Id
            );
        Assert.Null(unknownSubjectResponse.SubjectName);
        Assert.Equal(10, unknownSubjectResponse.ActualStudyMinutes);

        Assert.Equal(new DateOnly(2026, 9, 14), result.Value.WeeklyStudyPulse.StartsOn);
        Assert.Equal(new DateOnly(2026, 9, 20), result.Value.WeeklyStudyPulse.EndsOn);
        Assert.Equal(70, result.Value.WeeklyStudyPulse.ActualStudyMinutes);
        Assert.Equal(15, result.Value.WeeklyStudyPulse.PreviousPeriodActualStudyMinutes);
        Assert.Equal(55, result.Value.WeeklyStudyPulse.ActualStudyMinutesTrend);
        Assert.Equal(7, result.Value.WeeklyStudyPulse.Days.Count);
        Assert.Equal(
            30,
            result
                .Value.WeeklyStudyPulse.Days.Single(day => day.Date == new DateOnly(2026, 9, 18))
                .ActualStudyMinutes
        );
        Assert.Equal(2, result.Value.CompletedSessionsCount);
        Assert.Equal(3, result.Value.ActiveStudyDaysCount);
        Assert.NotNull(result.Value.CurrentStudyGoalProgress);
        Assert.Equal(0, result.Value.CurrentStudyGoalProgress.CompletedMinutes);
        Assert.Equal(0, result.Value.CurrentStudyGoalProgress.CompletionPercentage);
        Assert.Equal(0, result.Value.CurrentStudyGoalProgress.DaysRemaining);
    }

    [Fact]
    public async Task Handle_ForDailyGoalOnSelectedDay_ReturnsGoalProgress()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        Student student = new(Guid.NewGuid());
        student.SetParentSharingPreferences(true, true);
        StudentSubject math = StudentSubject.Predefined(StudentSubjectType.Math);
        student.Subjects.Add(math);
        student.SetStudyTimeGoal(
            StudyTimeGoal
                .Create(
                    StudyTimeGoalPeriod.Daily,
                    40,
                    [DayOfWeek.Sunday],
                    new DateOnly(2026, 9, 10)
                )
                .Value
        );

        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();

        StudySession session = CreatePausedCompletedSession(
            student.Id,
            math.Id,
            new DateTimeOffset(2026, 9, 20, 9, 0, 0, TimeSpan.Zero),
            30
        );
        StudySession previousDaySession = CreateCompletedSession(
            student.Id,
            math.Id,
            new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero),
            30
        );

        GetParentDashboardQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudyGoalProposal>(),
            new InMemoryRepository<StudySession>(session, previousDaySession),
            new InMemoryRepository<StudySessionBehaviorWindow>(),
            new FakeCurrentUser(parentUserId),
            new FixedTimeProvider(Now)
        );

        Result<ParentDashboardResponse> result = await handler.Handle(
            new GetParentDashboardQuery(student.Id),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.CurrentStudyGoalProgress);
        Assert.Equal(30, result.Value.CurrentStudyGoalProgress.CompletedMinutes);
        Assert.Equal(75m, result.Value.CurrentStudyGoalProgress.CompletionPercentage);
        Assert.Equal(1, result.Value.CurrentStudyGoalProgress.DaysRemaining);
    }

    [Fact]
    public async Task Handle_ForWeeklyGoal_UsesSelectedWeekStartDayForCurrentPeriod()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId, DayOfWeek.Saturday);
        parent.SetPrivateProperty(
            "User",
            new ApplicationUser { FirstName = "Mona", LastName = "Hassan" }
        );
        Student student = new(Guid.NewGuid());
        student.SetParentSharingPreferences(true, true);
        StudentSubject math = StudentSubject.Predefined(StudentSubjectType.Math);
        student.Subjects.Add(math);
        student.SetStudyTimeGoal(
            StudyTimeGoal
                .Create(
                    StudyTimeGoalPeriod.Weekly,
                    100,
                    [DayOfWeek.Saturday],
                    new DateOnly(2026, 9, 12)
                )
                .Value
        );

        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        StudyGoalProposal acceptedProposal = new(
            parent.Id,
            student.Id,
            StudyTimeGoal
                .Create(
                    StudyTimeGoalPeriod.Weekly,
                    100,
                    [DayOfWeek.Saturday],
                    new DateOnly(2026, 9, 12)
                )
                .Value
        );
        acceptedProposal.Accept(new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero));
        StudySession previousWeekSession = CreateCompletedSession(
            student.Id,
            math.Id,
            new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.Zero),
            40
        );
        StudySession periodStartSession = CreateCompletedSession(
            student.Id,
            math.Id,
            new DateTimeOffset(2026, 9, 19, 10, 0, 0, TimeSpan.Zero),
            30
        );
        StudySession crossingMidnightSession = CreateCompletedSession(
            student.Id,
            math.Id,
            new DateTimeOffset(2026, 9, 20, 23, 50, 0, TimeSpan.Zero),
            20
        );

        GetParentDashboardQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudyGoalProposal>(acceptedProposal),
            new InMemoryRepository<StudySession>(
                previousWeekSession,
                periodStartSession,
                crossingMidnightSession
            ),
            new InMemoryRepository<StudySessionBehaviorWindow>(),
            new FakeCurrentUser(parentUserId),
            new FixedTimeProvider(Now)
        );

        Result<ParentDashboardResponse> result = await handler.Handle(
            new GetParentDashboardQuery(student.Id),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.CurrentStudyGoalProgress);
        Assert.Equal(50, result.Value.CurrentStudyGoalProgress.CompletedMinutes);
        Assert.Equal(50m, result.Value.CurrentStudyGoalProgress.CompletionPercentage);
        Assert.Equal(6, result.Value.CurrentStudyGoalProgress.DaysRemaining);
        Assert.Equal(new DateOnly(2026, 9, 19), result.Value.CurrentStudyGoalProgress.StartsOn);
        Assert.Equal(new DateOnly(2026, 9, 25), result.Value.CurrentStudyGoalProgress.EndsOn);
        Assert.Equal(7, result.Value.CurrentStudyGoalProgress.Days.Count);
        Assert.Equal(
            30,
            result
                .Value.CurrentStudyGoalProgress.Days.Single(day =>
                    day.Date == new DateOnly(2026, 9, 19)
                )
                .ActualStudyMinutes
        );
        Assert.Equal(
            0,
            result
                .Value.CurrentStudyGoalProgress.Days.Single(day =>
                    day.Date == new DateOnly(2026, 9, 21)
                )
                .ActualStudyMinutes
        );
        Assert.NotNull(result.Value.CurrentStudyGoalAcceptedProposal);
        Assert.Equal(acceptedProposal.Id, result.Value.CurrentStudyGoalAcceptedProposal.Id);
        Assert.Equal("Accepted", result.Value.CurrentStudyGoalAcceptedProposal.Status);
        Assert.NotNull(result.Value.CurrentStudyGoalAcceptedProposal.RespondedAtUtc);
        Assert.Equal(
            "Mona Hassan",
            result.Value.CurrentStudyGoalAcceptedProposal.SuggestedByParentName
        );
    }

    [Fact]
    public async Task Handle_ForWeeklyGoalOnWeekStart_IncludesFullWeekAndReportsSevenDaysRemaining()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId, DayOfWeek.Saturday);
        Student student = new(Guid.NewGuid());
        student.SetParentSharingPreferences(true, true);
        StudentSubject math = StudentSubject.Predefined(StudentSubjectType.Math);
        student.Subjects.Add(math);
        student.SetStudyTimeGoal(
            StudyTimeGoal
                .Create(
                    StudyTimeGoalPeriod.Weekly,
                    100,
                    [DayOfWeek.Saturday],
                    new DateOnly(2026, 9, 12)
                )
                .Value
        );

        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        StudySession priorDaySession = CreateCompletedSession(
            student.Id,
            math.Id,
            new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.Zero),
            40
        );
        StudySession weekStartSession = CreateCompletedSession(
            student.Id,
            math.Id,
            new DateTimeOffset(2026, 9, 19, 10, 0, 0, TimeSpan.Zero),
            30
        );

        GetParentDashboardQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudyGoalProposal>(),
            new InMemoryRepository<StudySession>(priorDaySession, weekStartSession),
            new InMemoryRepository<StudySessionBehaviorWindow>(),
            new FakeCurrentUser(parentUserId),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero))
        );

        Result<ParentDashboardResponse> result = await handler.Handle(
            new GetParentDashboardQuery(student.Id),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.CurrentStudyGoalProgress);
        Assert.Equal(30, result.Value.CurrentStudyGoalProgress.CompletedMinutes);
        Assert.Equal(30m, result.Value.CurrentStudyGoalProgress.CompletionPercentage);
        Assert.Equal(7, result.Value.CurrentStudyGoalProgress.DaysRemaining);
    }

    [Fact]
    public async Task Handle_ForWeeklyGoalOnFinalDay_IncludesPeriodEndAndReportsOneDayRemaining()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId, DayOfWeek.Saturday);
        Student student = new(Guid.NewGuid());
        student.SetParentSharingPreferences(true, true);
        StudentSubject math = StudentSubject.Predefined(StudentSubjectType.Math);
        student.Subjects.Add(math);
        student.SetStudyTimeGoal(
            StudyTimeGoal
                .Create(
                    StudyTimeGoalPeriod.Weekly,
                    100,
                    [DayOfWeek.Saturday],
                    new DateOnly(2026, 9, 12)
                )
                .Value
        );

        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        StudySession periodEndSession = CreateCompletedSession(
            student.Id,
            math.Id,
            new DateTimeOffset(2026, 9, 25, 23, 50, 0, TimeSpan.Zero),
            20
        );
        StudySession nextWeekSession = CreateCompletedSession(
            student.Id,
            math.Id,
            new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero),
            40
        );

        GetParentDashboardQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudyGoalProposal>(),
            new InMemoryRepository<StudySession>(periodEndSession, nextWeekSession),
            new InMemoryRepository<StudySessionBehaviorWindow>(),
            new FakeCurrentUser(parentUserId),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero))
        );

        Result<ParentDashboardResponse> result = await handler.Handle(
            new GetParentDashboardQuery(student.Id),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.CurrentStudyGoalProgress);
        Assert.Equal(20, result.Value.CurrentStudyGoalProgress.CompletedMinutes);
        Assert.Equal(20m, result.Value.CurrentStudyGoalProgress.CompletionPercentage);
        Assert.Equal(1, result.Value.CurrentStudyGoalProgress.DaysRemaining);
    }

    [Fact]
    public async Task Handle_AttributesSessionsCrossingMidnightToStartedDate()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId, DayOfWeek.Sunday);
        Student student = new(Guid.NewGuid());
        student.SetParentSharingPreferences(true, true);
        StudentSubject math = StudentSubject.Predefined(StudentSubjectType.Math);
        student.Subjects.Add(math);
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        DateTimeOffset utcNow = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

        StudySession crossingMidnight = CreateCompletedSession(
            student.Id,
            math.Id,
            new DateTimeOffset(2026, 9, 20, 23, 50, 0, TimeSpan.Zero),
            20
        );

        GetParentDashboardQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudyGoalProposal>(),
            new InMemoryRepository<StudySession>(crossingMidnight),
            new InMemoryRepository<StudySessionBehaviorWindow>(),
            new FakeCurrentUser(parentUserId),
            new FixedTimeProvider(utcNow)
        );

        Result<ParentDashboardResponse> result = await handler.Handle(
            new GetParentDashboardQuery(student.Id),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(
            20,
            result
                .Value.WeeklyStudyPulse.Days.Single(day => day.Date == new DateOnly(2026, 9, 20))
                .ActualStudyMinutes
        );
        Assert.Equal(
            0,
            result
                .Value.WeeklyStudyPulse.Days.Single(day => day.Date == new DateOnly(2026, 9, 21))
                .ActualStudyMinutes
        );
    }

    [Fact]
    public async Task Handle_WhenRelationshipIsInactive_ReturnsForbidden()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        Student student = new(Guid.NewGuid());
        student.SetParentSharingPreferences(true, true);
        ParentStudentRelationship relationship = new(parent.Id, student.Id);

        GetParentDashboardQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudyGoalProposal>(),
            new InMemoryRepository<StudySession>(),
            new InMemoryRepository<StudySessionBehaviorWindow>(),
            new FakeCurrentUser(parentUserId),
            new FixedTimeProvider(Now)
        );

        Result<ParentDashboardResponse> result = await handler.Handle(
            new GetParentDashboardQuery(student.Id),
            CancellationToken.None
        );

        Assert.True(result.IsError);
        Assert.Equal(ErrorKind.Forbidden, result.TopError.Type);
        Assert.Equal("ParentDashboard.RelationshipRequired", result.TopError.Code);
    }

    [Fact]
    public async Task Handle_WhenParentHasNotSelectedWeekStart_ReturnsValidationError()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        Student student = new(Guid.NewGuid());
        student.SetParentSharingPreferences(true, true);
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();

        GetParentDashboardQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudyGoalProposal>(),
            new InMemoryRepository<StudySession>(),
            new InMemoryRepository<StudySessionBehaviorWindow>(),
            new FakeCurrentUser(parentUserId),
            new FixedTimeProvider(Now)
        );

        Result<ParentDashboardResponse> result = await handler.Handle(
            new GetParentDashboardQuery(student.Id),
            CancellationToken.None
        );

        Assert.True(result.IsError);
        Assert.Equal(ErrorKind.Validation, result.TopError.Type);
        Assert.Equal("ParentDashboard.WeekStartsOnRequired", result.TopError.Code);
    }

    [Fact]
    public async Task Sessions_AppliesMultipleStatusSubjectFiltersAndPaginationWithDeterministicOrdering()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        Student student = new(Guid.NewGuid());
        student.SetParentSharingPreferences(true, true);
        StudentSubject math = StudentSubject.Predefined(StudentSubjectType.Math);
        StudentSubject english = StudentSubject.Predefined(StudentSubjectType.English);
        student.Subjects.Add(math);
        student.Subjects.Add(english);
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();

        DateTimeOffset sharedStart = new(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);
        StudySession completedLowId = CreateCompletedSession(student.Id, math.Id, sharedStart, 20);
        StudySession completedHighId = CreateCompletedSession(student.Id, math.Id, sharedStart, 25);
        StudySession paused = CreatePausedSession(
            student.Id,
            math.Id,
            new DateTimeOffset(2026, 9, 19, 10, 0, 0, TimeSpan.Zero),
            30
        );
        StudySession englishCompleted = CreateCompletedSession(
            student.Id,
            english.Id,
            new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.Zero),
            30
        );
        StudySession draft = StudySession.Create(student.Id, StudySessionMode.Digital).Value;

        GetParentDashboardSessionsQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudySession>(
                completedLowId,
                completedHighId,
                paused,
                englishCompleted,
                draft
            ),
            new FakeCurrentUser(parentUserId),
            new FixedTimeProvider(Now)
        );

        Result<ParentDashboardSessionHistoryResponse> result = await handler.Handle(
            new GetParentDashboardSessionsQuery(
                student.Id,
                ["Completed", "Paused"],
                math.Id,
                Page: 1,
                PageSize: 2
            ),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Pagination.TotalCount);
        Assert.Equal(2, result.Value.Pagination.TotalPages);
        Assert.Equal(1, result.Value.Pagination.Page);
        Assert.Equal(2, result.Value.Pagination.PageSize);
        Assert.Equal(
            new[] { completedLowId.Id, completedHighId.Id }.OrderByDescending(id => id),
            result.Value.Sessions.Select(session => session.Id)
        );
    }

    [Fact]
    public async Task Sessions_WhenNoFiltersSupplied_ReturnsNewestPageOnly()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        Student student = new(Guid.NewGuid());
        student.SetParentSharingPreferences(true, true);
        StudentSubject math = StudentSubject.Predefined(StudentSubjectType.Math);
        student.Subjects.Add(math);
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        StudySession[] sessions = Enumerable
            .Range(0, 3)
            .Select(index =>
                CreateCompletedSession(
                    student.Id,
                    math.Id,
                    new DateTimeOffset(2026, 9, 18 + index, 10, 0, 0, TimeSpan.Zero),
                    20
                )
            )
            .ToArray();

        GetParentDashboardSessionsQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudySession>(sessions),
            new FakeCurrentUser(parentUserId),
            new FixedTimeProvider(Now)
        );

        Result<ParentDashboardSessionHistoryResponse> result = await handler.Handle(
            new GetParentDashboardSessionsQuery(student.Id, null, null, Page: 1, PageSize: 2),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Pagination.TotalCount);
        Assert.Equal(2, result.Value.Sessions.Count);
        Assert.Equal(
            [sessions[2].Id, sessions[1].Id],
            result.Value.Sessions.Select(session => session.Id)
        );
    }

    [Fact]
    public async Task Sessions_WithInvalidFilters_ReturnsValidationErrors()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        Student student = new(Guid.NewGuid());
        student.SetParentSharingPreferences(true, true);
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();

        GetParentDashboardSessionsQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudySession>(),
            new FakeCurrentUser(parentUserId),
            new FixedTimeProvider(Now)
        );

        Result<ParentDashboardSessionHistoryResponse> result = await handler.Handle(
            new GetParentDashboardSessionsQuery(
                student.Id,
                ["Finished"],
                Guid.Empty,
                Page: 0,
                PageSize: 101
            ),
            CancellationToken.None
        );

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "ParentDashboard.InvalidPage");
        Assert.Contains(result.Errors, error => error.Code == "ParentDashboard.InvalidPageSize");
        Assert.Contains(result.Errors, error => error.Code == "ParentDashboard.InvalidSubjectId");
        Assert.Contains(
            result.Errors,
            error => error.Code == "ParentDashboard.InvalidSessionStatus"
        );
    }

    [Fact]
    public async Task Export_AppliesFiltersToFullHistoryAndEscapesCsv()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        Student student = new(Guid.NewGuid());
        student.SetParentSharingPreferences(true, true);
        StudentSubject custom = StudentSubject.Custom("Chemistry, \"Organic\"\nLab");
        student.Subjects.Add(custom);
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();

        StudySession[] completed = Enumerable
            .Range(0, 3)
            .Select(index =>
                CreateCompletedSession(
                    student.Id,
                    custom.Id,
                    new DateTimeOffset(2026, 9, 17 + index, 10, 0, 0, TimeSpan.Zero),
                    20
                )
            )
            .ToArray();
        StudySession active = CreateActiveSession(
            student.Id,
            custom.Id,
            new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero),
            20
        );

        ExportParentDashboardSessionsQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudySession>([.. completed, active]),
            new FakeCurrentUser(parentUserId),
            new FixedTimeProvider(Now)
        );

        Result<ParentDashboardSessionsCsvExport> result = await handler.Handle(
            new ExportParentDashboardSessionsQuery(student.Id, ["Completed"], custom.Id),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal("text/csv; charset=utf-8", result.Value.ContentType);
        Assert.StartsWith("parent-dashboard-sessions-", result.Value.FileName);

        string csv = Encoding.UTF8.GetString(result.Value.Content);
        Assert.Contains("Id,StartedAtUtc,Status,Mode,SelectedSubjectId,SubjectName", csv);
        Assert.Equal(
            3,
            completed.Count(session =>
                csv.Contains(session.Id.ToString(), StringComparison.Ordinal)
            )
        );
        Assert.DoesNotContain(active.Id.ToString(), csv);
        Assert.Contains("\"Chemistry, \"\"Organic\"\"\nLab\"", csv);
    }

    [Theory]
    [InlineData("=SUM(A1:A2)", "'=SUM(A1:A2)")]
    [InlineData("+SUM(A1:A2)", "'+SUM(A1:A2)")]
    [InlineData("-SUM(A1:A2)", "'-SUM(A1:A2)")]
    [InlineData("@SUM(A1:A2)", "'@SUM(A1:A2)")]
    public async Task Export_PrefixesFormulaLikeSubjectNamesWithApostrophe(
        string subjectName,
        string expectedCsvValue
    )
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        Student student = new(Guid.NewGuid());
        student.SetParentSharingPreferences(true, true);
        StudentSubject custom = StudentSubject.Custom(subjectName);
        student.Subjects.Add(custom);
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        StudySession session = CreateCompletedSession(
            student.Id,
            custom.Id,
            new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero),
            20
        );

        ExportParentDashboardSessionsQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudySession>(session),
            new FakeCurrentUser(parentUserId),
            new FixedTimeProvider(Now)
        );

        Result<ParentDashboardSessionsCsvExport> result = await handler.Handle(
            new ExportParentDashboardSessionsQuery(student.Id, null, null),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);

        string csv = Encoding.UTF8.GetString(result.Value.Content);
        Assert.Contains($",{expectedCsvValue},", csv);
        Assert.DoesNotContain($",{subjectName},", csv);
    }

    [Fact]
    public async Task Export_EscapesFormulaLikeSubjectNamesAfterPrefixingApostrophe()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);
        Student student = new(Guid.NewGuid());
        student.SetParentSharingPreferences(true, true);
        StudentSubject custom = StudentSubject.Custom("=\"Quoted\", Cell\nLine");
        student.Subjects.Add(custom);
        ParentStudentRelationship relationship = new(parent.Id, student.Id);
        relationship.Accept();
        StudySession session = CreateCompletedSession(
            student.Id,
            custom.Id,
            new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero),
            20
        );

        ExportParentDashboardSessionsQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(relationship),
            new InMemoryRepository<Student>(student),
            new InMemoryRepository<StudySession>(session),
            new FakeCurrentUser(parentUserId),
            new FixedTimeProvider(Now)
        );

        Result<ParentDashboardSessionsCsvExport> result = await handler.Handle(
            new ExportParentDashboardSessionsQuery(student.Id, null, null),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);

        string csv = Encoding.UTF8.GetString(result.Value.Content);
        Assert.Contains("\"'=\"\"Quoted\"\", Cell\nLine\"", csv);
    }

    [Fact]
    public async Task Handle_WhenRelationshipIsMissing_ReturnsForbidden()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = CreateParent(parentUserId);

        GetParentDashboardQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ParentStudentRelationship>(),
            new InMemoryRepository<Student>(),
            new InMemoryRepository<StudyGoalProposal>(),
            new InMemoryRepository<StudySession>(),
            new InMemoryRepository<StudySessionBehaviorWindow>(),
            new FakeCurrentUser(parentUserId),
            new FixedTimeProvider(Now)
        );

        Result<ParentDashboardResponse> result = await handler.Handle(
            new GetParentDashboardQuery(Guid.NewGuid()),
            CancellationToken.None
        );

        Assert.True(result.IsError);
        Assert.Equal(ErrorKind.Forbidden, result.TopError.Type);
        Assert.Equal("ParentDashboard.RelationshipRequired", result.TopError.Code);
    }

    private static StudySession CreateCompletedSession(
        Guid studentId,
        Guid subjectId,
        DateTimeOffset startedAtUtc,
        int durationMinutes
    )
    {
        StudySession session = CreateReadySession(studentId, subjectId, durationMinutes);
        Assert.True(session.Start(startedAtUtc).IsSuccess);
        Assert.True(
            session.CompleteSuccessfully(startedAtUtc.AddMinutes(durationMinutes)).IsSuccess
        );
        return session;
    }

    private static StudySession CreatePausedCompletedSession(
        Guid studentId,
        Guid subjectId,
        DateTimeOffset startedAtUtc,
        int durationMinutes
    )
    {
        StudySession session = CreateReadySession(studentId, subjectId, durationMinutes);
        Assert.True(session.Start(startedAtUtc).IsSuccess);
        Assert.True(session.Pause(startedAtUtc.AddMinutes(10)).IsSuccess);
        Assert.True(session.Resume(startedAtUtc.AddMinutes(20)).IsSuccess);
        Assert.True(session.CompleteSuccessfully(startedAtUtc.AddMinutes(40)).IsSuccess);
        return session;
    }

    private static StudySession CreateActiveSession(
        Guid studentId,
        Guid subjectId,
        DateTimeOffset startedAtUtc,
        int durationMinutes
    )
    {
        StudySession session = CreateReadySession(studentId, subjectId, durationMinutes);
        Assert.True(session.Start(startedAtUtc).IsSuccess);
        return session;
    }

    private static StudySession CreatePausedSession(
        Guid studentId,
        Guid subjectId,
        DateTimeOffset startedAtUtc,
        int durationMinutes
    )
    {
        StudySession session = CreateReadySession(studentId, subjectId, durationMinutes);
        Assert.True(session.Start(startedAtUtc).IsSuccess);
        Assert.True(session.Pause(startedAtUtc.AddMinutes(10)).IsSuccess);
        return session;
    }

    private static StudySession CreateReadySession(
        Guid studentId,
        Guid subjectId,
        int durationMinutes
    )
    {
        StudySession session = StudySession.Create(studentId, StudySessionMode.Digital).Value;
        StudyMaterial material = StudyMaterial
            .Create(
                studentId,
                "book.pdf",
                1024,
                50,
                $"materials/{Guid.NewGuid()}/book.pdf",
                StudyMaterialSource.Upload
            )
            .Value;
        StudyMaterialSection section = StudyMaterialSection
            .Create(material.Id, "Chapter 1", durationMinutes, 1, 5)
            .Value;

        Assert.True(session.SetSubjectId(subjectId).IsSuccess);
        Assert.True(session.SetDuration(durationMinutes).IsSuccess);
        Assert.True(session.SetStudyMaterial(material).IsSuccess);
        Assert.True(
            session
                .SetSelection(StudySessionSelection.Create(session, material, 1, 5).Value)
                .IsSuccess
        );
        Assert.True(session.SetSelectedSections([section]).IsSuccess);
        Assert.True(session.MarkReady().IsSuccess);

        return session;
    }

    private static Parent CreateParent(Guid userId, DayOfWeek weekStartsOn = DayOfWeek.Monday)
    {
        Parent parent = new(userId);
        parent.SetWeekStartsOn(weekStartsOn);
        return parent;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
    [Fact]
public async Task Handle_WhenBehaviorScoresExist_ReturnsRoundedAverageFocusQuality()
{
    Guid parentUserId = Guid.NewGuid();
    Parent parent = CreateParent(parentUserId);
    Student student = new(Guid.NewGuid());
    student.SetParentSharingPreferences(true, true);

    ParentStudentRelationship relationship = new(parent.Id, student.Id);
    relationship.Accept();

    StudySession session = CreateCompletedSession(
        student.Id,
        Guid.NewGuid(),
        new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero),
        30
    );

    StudySessionBehaviorWindow firstWindow = StudySessionBehaviorWindow.Create(
        session.Id, 1, Now.AddMinutes(-30), Now.AddMinutes(-15), false
    ).Value;
    Assert.True(firstWindow.RecordAnalysis(70, null, null, null, null, null, null, false).IsSuccess);

    StudySessionBehaviorWindow secondWindow = StudySessionBehaviorWindow.Create(
        session.Id, 2, Now.AddMinutes(-15), Now, true
    ).Value;
    Assert.True(secondWindow.RecordAnalysis(81, null, null, null, null, null, null, false).IsSuccess);

    GetParentDashboardQueryHandler handler = new(
        new InMemoryRepository<Parent>(parent),
        new InMemoryRepository<ParentStudentRelationship>(relationship),
        new InMemoryRepository<Student>(student),
        new InMemoryRepository<StudyGoalProposal>(),
        new InMemoryRepository<StudySession>(session),
        new InMemoryRepository<StudySessionBehaviorWindow>(firstWindow, secondWindow),
        new FakeCurrentUser(parentUserId),
        new FixedTimeProvider(Now)
    );

    Result<ParentDashboardResponse> result = await handler.Handle(
        new GetParentDashboardQuery(student.Id),
        CancellationToken.None
    );

    Assert.True(result.IsSuccess);
    Assert.Equal(76, result.Value.FocusQuality);
}

[Fact]
public async Task Handle_WhenParentSummarySharingIsDisabled_ReturnsNullFocusQuality()
{
    Guid parentUserId = Guid.NewGuid();
    Parent parent = CreateParent(parentUserId);
    Student student = new(Guid.NewGuid());
    student.SetParentSharingPreferences(true, false);

    ParentStudentRelationship relationship = new(parent.Id, student.Id);
    relationship.Accept();

    StudySession session = CreateCompletedSession(
        student.Id,
        Guid.NewGuid(),
        new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero),
        30
    );

    StudySessionBehaviorWindow window = StudySessionBehaviorWindow.Create(
        session.Id, 1, Now.AddMinutes(-30), Now, true
    ).Value;
    Assert.True(window.RecordAnalysis(90, null, null, null, null, null, null, false).IsSuccess);

    GetParentDashboardQueryHandler handler = new(
        new InMemoryRepository<Parent>(parent),
        new InMemoryRepository<ParentStudentRelationship>(relationship),
        new InMemoryRepository<Student>(student),
        new InMemoryRepository<StudyGoalProposal>(),
        new InMemoryRepository<StudySession>(session),
        new InMemoryRepository<StudySessionBehaviorWindow>(window),
        new FakeCurrentUser(parentUserId),
        new FixedTimeProvider(Now)
    );

    Result<ParentDashboardResponse> result = await handler.Handle(
        new GetParentDashboardQuery(student.Id),
        CancellationToken.None
    );

    Assert.True(result.IsSuccess);
    Assert.Null(result.Value.FocusQuality);
}
}

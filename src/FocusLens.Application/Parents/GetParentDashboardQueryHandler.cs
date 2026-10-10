using System.Globalization;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Common.Services;
using FocusLens.Application.Common.Utilities;
using FocusLens.Application.Progress;
using FocusLens.Contracts;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using FocusLens.Domain.StudySessions;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Parents;

public sealed class GetParentDashboardQueryHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudyGoalProposal> studyGoalProposalRepository,
    IBaseRepository<StudySession> studySessionRepository,
    IBaseRepository<StudySessionBehaviorWindow> behaviorWindowRepository,
    IBaseRepository<StudentWeek> weekRepository,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IUnitOfWork? unitOfWork = null,
    IStudentLocalTime? studentLocalTime = null)
    : IRequestHandler<GetParentDashboardQuery, Result<ParentDashboardResponse>>
{
    public async Task<Result<ParentDashboardResponse>> Handle(
        GetParentDashboardQuery request,
        CancellationToken cancellationToken)
    {
        studentLocalTime ??= new StudentLocalTime(timeProvider);
        // Goal reads live in the dedicated Study Goals query.
        _ = studyGoalProposalRepository;

        Result<ParentDashboardContext> contextResult = await ParentDashboardHelpers.ResolveContextAsync(
            request.StudentId,
            parentRepository,
            relationshipRepository,
            studentRepository,
            currentUser);

        if (contextResult.IsError)
        {
            return contextResult.Errors;
        }

        Student student = contextResult.Value.Student;
        bool shareTrends = student.ShareSubjectTrendsWithParents;
        Result<ParentOverviewWeekResponse> resolvedWeek =
            await OverviewWeekResolver.ResolveAsync(contextResult.Value, studentLocalTime, weekRepository, unitOfWork);
        if (resolvedWeek.IsError)
        {
            return resolvedWeek.Errors;
        }

        DateTimeOffset utcNow = timeProvider.GetUtcNow();
        DateOnly weekStart = resolvedWeek.Value.StartsOn;
        DateOnly weekEnd = resolvedWeek.Value.EndsOn;
        StudySession[] startedSessions = (await studySessionRepository.GetAllAsync(session =>
                    session.StudentId == student.Id &&
                    session.StartedAtUtc != null,
                session => session.PauseIntervals))
            .ToArray();
        ParentDashboardWeeklyStudyPulseResponse weeklyPulse =
            await CreateWeeklyPulse(startedSessions, utcNow, weekStart, weekEnd, student, studentLocalTime);
        ParentDashboardFocusPatternResponse focusPattern = CreateFocusPattern(
            startedSessions,
            utcNow,
            weekStart,
            shareTrends, student, studentLocalTime);
        return new ParentDashboardResponse(
            student.Id,
            new ParentDashboardWeekResponse(weekStart, weekEnd, resolvedWeek.Value.IsCurrentWeek),
            shareTrends
                ? weeklyPulse
                : weeklyPulse with
                {
                    TotalStudyMinutes = FormatMinutes(0),
                    PreviousWeekStudyMinutes = FormatMinutes(0),
                    StudyMinutesTrend = FormatMinutes(0),
                    PeakDay = null,
                    TotalSessions = 0,
                    CompletedSessions = 0,
                    ActiveStudyDays = 0,
                    FocusQuality = new ParentDashboardFocusQualityResponse(null),
                    Days = weeklyPulse.Days.Select(day => day with { StudyMinutes = FormatMinutes(0) }).ToArray()
                },
            focusPattern);
    }

    private async Task<ParentDashboardWeeklyStudyPulseResponse> CreateWeeklyPulse(
        IReadOnlyCollection<StudySession> sessions,
        DateTimeOffset utcNow,
        DateOnly startsOn,
        DateOnly endsOn,
        Student student,
        IStudentLocalTime studentLocalTime)
    {
        DateOnly previousStartsOn = startsOn.AddDays(-ParentDashboardHelpers.PulseDays);
        DateOnly previousEndsOn = previousStartsOn.AddDays(ParentDashboardHelpers.PulseDays - 1);

        (DateOnly Date, string Day, int StudyMinutes)[] dailyMinutes = Enumerable
            .Range(0, ParentDashboardHelpers.PulseDays)
            .Select(offset =>
            {
                DateOnly date = startsOn.AddDays(offset);
                return (
                    date,
                    date.DayOfWeek.ToString(),
                    ParentDashboardHelpers.GetActualStudyMinutesForDates(
                        sessions, date, date, utcNow, student, studentLocalTime));
            })
            .ToArray();

        int actualStudyMinutes = dailyMinutes.Sum(day => day.StudyMinutes);
        int previousPeriodActualStudyMinutes = ParentDashboardHelpers.GetActualStudyMinutesForDates(
            sessions, previousStartsOn, previousEndsOn, utcNow, student, studentLocalTime);

        StudySession[] selectedSessions = sessions
            .Where(session => IsInWeek(session, startsOn, endsOn, student, studentLocalTime)).ToArray();
        Guid[] selectedSessionIds = selectedSessions.Select(session => session.Id).ToArray();
        StudySessionBehaviorWindow[] windows = selectedSessionIds.Length == 0
            ? []
            : (await behaviorWindowRepository.GetAllAsync(window => selectedSessionIds.Contains(window.StudySessionId)))
            .ToArray();
        string? focusTrend = FocusQualityTrendCalculator.Calculate(windows, startsOn, endsOn,
            window => studentLocalTime.GetLocalDate(window.WindowEndUtc, student));
        ParentDashboardWeeklyStudyPulseDayResponse[] days = dailyMinutes
            .Select(day => new ParentDashboardWeeklyStudyPulseDayResponse(
                day.Date, day.Day, FormatMinutes(day.StudyMinutes)))
            .ToArray();
        (DateOnly Date, string Day, int StudyMinutes)? peakDay = dailyMinutes
            .Where(day => day.StudyMinutes > 0)
            .OrderByDescending(day => day.StudyMinutes)
            .ThenBy(day => day.Date)
            .Select(day => ((DateOnly Date, string Day, int StudyMinutes)?)day)
            .FirstOrDefault();
        return new ParentDashboardWeeklyStudyPulseResponse(
            FormatMinutes(actualStudyMinutes),
            FormatMinutes(previousPeriodActualStudyMinutes),
            FormatMinutes(actualStudyMinutes - previousPeriodActualStudyMinutes),
            days,
            peakDay is null
                ? null
                : new ParentDashboardWeeklyStudyPulseDayResponse(
                    peakDay.Value.Date, peakDay.Value.Day, FormatMinutes(peakDay.Value.StudyMinutes)),
            selectedSessions.Length,
            selectedSessions.Count(session => session.Status == StudySessionStatus.Completed),
            selectedSessions.Select(session => studentLocalTime.GetLocalDate(session.StartedAtUtc!.Value, student))
                .Distinct().Count(),
            new ParentDashboardFocusQualityResponse(focusTrend));
    }

    private static bool IsInWeek(StudySession session, DateOnly startsOn, DateOnly endsOn, Student student,
        IStudentLocalTime studentLocalTime) =>
        studentLocalTime.GetLocalDate(session.StartedAtUtc!.Value, student) is DateOnly date && date >= startsOn &&
        date <= endsOn;

    private static ParentDashboardFocusPatternResponse CreateFocusPattern(
        IReadOnlyCollection<StudySession> sessions,
        DateTimeOffset utcNow,
        DateOnly weekStart,
        bool includeStudyTiming,
        Student student,
        IStudentLocalTime studentLocalTime)
    {
        StudySession[] selectedSessions = includeStudyTiming
            ? sessions.Where(session => IsInWeek(session, weekStart,
                weekStart.AddDays(ParentDashboardHelpers.PulseDays - 1), student, studentLocalTime)).ToArray()
            : [];

        ParentDashboardFocusPatternDayResponse[] days = Enumerable
            .Range(0, ParentDashboardHelpers.PulseDays)
            .Select(offset =>
            {
                DateOnly date = weekStart.AddDays(offset);
                ParentDashboardFocusPatternSessionResponse[] daySessions = selectedSessions
                    .Where(session => studentLocalTime.GetLocalDate(session.StartedAtUtc!.Value, student) == date)
                    .OrderBy(session => session.StartedAtUtc)
                    .ThenBy(session => session.Id)
                    .Select(session => new ParentDashboardFocusPatternSessionResponse(
                        FormatStartTime(studentLocalTime.ConvertFromUtc(session.StartedAtUtc!.Value, student)),
                        FormatMinutes(ParentDashboardHelpers.GetActualStudyMinutes(session, utcNow))))
                    .ToArray();

                return new ParentDashboardFocusPatternDayResponse(date.DayOfWeek.ToString(), daySessions);
            })
            .ToArray();

        int? mostActiveHour = selectedSessions
            .GroupBy(session => studentLocalTime.ConvertFromUtc(session.StartedAtUtc!.Value, student).Hour)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => (int?)group.Key)
            .FirstOrDefault();

        ParentDashboardFocusPatternTimeBlockResponse? mostActiveTimeBlock = mostActiveHour is int hour
            ? new ParentDashboardFocusPatternTimeBlockResponse(
                FormatHour(hour),
                FormatHour((hour + 2) % 24))
            : null;

        return new ParentDashboardFocusPatternResponse(days, mostActiveTimeBlock);
    }

    private static string FormatStartTime(DateTimeOffset value) =>
        value.UtcDateTime.ToString("h tt", CultureInfo.InvariantCulture);

    private static string FormatHour(int hour) =>
        new DateTime(2000, 1, 1, hour, 0, 0, DateTimeKind.Utc)
            .ToString("h tt", CultureInfo.InvariantCulture);

    private static string FormatMinutes(int minutes) => DurationDisplayFormatter.FormatMinutes(minutes);
}
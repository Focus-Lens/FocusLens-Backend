using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Common.Utilities;
using FocusLens.Contracts.Home;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Students;
using FocusLens.Domain.StudySessions;
using FocusLens.Application.Parents;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Home;

public sealed class HomeQueryHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudySession> studySessionRepository,
    ICurrentUser currentUser,
    IStudentLocalTime studentLocalTime,
    TimeProvider timeProvider)
    : IRequestHandler<GetStudyOverviewQuery, StudyOverviewResponse?>,
        IRequestHandler<GetSessionsByDayQuery, SessionsByDayResponse?>
{
    public async Task<SessionsByDayResponse?> Handle(GetSessionsByDayQuery request,
        CancellationToken cancellationToken)
    {
        (Student Student, IReadOnlyCollection<StudySession> Sessions, IReadOnlyCollection<DateOnly> SessionDates)? data =
            await GetSessionDatesAsync();
        if (data is null)
        {
            return null;
        }

        return new SessionsByDayResponse(data.Value.SessionDates
            .GroupBy(date => date)
            .OrderBy(group => group.Key)
            .Select(group => new SessionsByDayItemResponse(group.Key, group.Count()))
            .ToArray());
    }

    public async Task<StudyOverviewResponse?> Handle(GetStudyOverviewQuery request,
        CancellationToken cancellationToken)
    {
        (Student Student, IReadOnlyCollection<StudySession> Sessions, IReadOnlyCollection<DateOnly> SessionDates)? data =
            await GetSessionDatesAsync(includePauseIntervals: true);
        if (data is null)
        {
            return null;
        }

        DateOnly today = studentLocalTime.GetToday(data.Value.Student);
        DateOnly weekStart = WeekStartCalculator.GetWeekStart(today,
            data.Value.Student.WeekStartsOn ?? DayOfWeek.Saturday);
        StudySession[] completedSessions = data.Value.Sessions
            .Where(session => session.Status == StudySessionStatus.Completed &&
                              session.CompletedAtUtc is not null &&
                              session.CompletedAtUtc > session.StartedAtUtc)
            .ToArray();
        int focusTimeMinutes = ParentDashboardHelpers.GetActualStudyMinutesForDates(
            completedSessions, weekStart, today, timeProvider.GetUtcNow(), data.Value.Student, studentLocalTime);
        StudyTimeGoal? goal = data.Value.Student.StudyTimeGoal;
        StudyTimeGoal? weeklyGoal = goal?.Period == StudyTimeGoalPeriod.Weekly && goal.IsActiveOn(today)
            ? goal
            : null;
        int weeklyStudiedMinutes = weeklyGoal is null
            ? 0
            : ParentDashboardHelpers.GetActualStudyMinutesForDates(
                completedSessions, weeklyGoal.StartDate!.Value, today, timeProvider.GetUtcNow(),
                data.Value.Student, studentLocalTime);
        decimal weeklyGoalPercentage = weeklyGoal is null || weeklyGoal.TargetMinutes <= 0
            ? 0
            : Math.Round(weeklyStudiedMinutes * 100m / weeklyGoal.TargetMinutes, 2);

        return new StudyOverviewResponse(
            GetStreakDays(data.Value.SessionDates, today),
            data.Value.SessionDates.Count(date => date == today),
            focusTimeMinutes,
            weeklyGoal is null ? null : FormatCompactMinutes(weeklyGoal.TargetMinutes),
            FormatCompactMinutes(weeklyStudiedMinutes),
            weeklyGoalPercentage,
            data.Value.Sessions.Count > 0);
    }

    private async Task<(Student Student, IReadOnlyCollection<StudySession> Sessions,
        IReadOnlyCollection<DateOnly> SessionDates)?> GetSessionDatesAsync(bool includePauseIntervals = false)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return null;
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(item => item.UserId == userId);
        if (student is null)
        {
            return null;
        }

        IEnumerable<StudySession> sessions = includePauseIntervals
            ? await studySessionRepository.GetAllAsync(item =>
                item.StudentId == student.Id && item.StartedAtUtc != null,
                item => item.PauseIntervals)
            : await studySessionRepository.GetAllAsync(item =>
                item.StudentId == student.Id && item.StartedAtUtc != null);
        StudySession[] sessionArray = sessions.ToArray();
        return (student, sessionArray, sessionArray
            .Select(item => studentLocalTime.GetLocalDate(item.StartedAtUtc!.Value, student))
            .ToArray());
    }

    private static int GetStreakDays(IReadOnlyCollection<DateOnly> sessionDates, DateOnly today)
    {
        HashSet<DateOnly> dates = sessionDates.ToHashSet();
        DateOnly current = today;
        int streak = 0;

        while (dates.Contains(current))
        {
            streak++;
            current = current.AddDays(-1);
        }

        return streak;
    }

    private static string FormatCompactMinutes(int minutes)
    {
        int nonNegativeMinutes = Math.Max(0, minutes);
        int hours = nonNegativeMinutes / 60;
        int remainingMinutes = nonNegativeMinutes % 60;

        return hours == 0
            ? $"{remainingMinutes}m"
            : remainingMinutes == 0
                ? $"{hours}h"
                : $"{hours}h {remainingMinutes}m";
    }
}

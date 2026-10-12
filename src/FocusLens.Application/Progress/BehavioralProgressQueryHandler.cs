using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Common.Services;
using FocusLens.Contracts.Progress;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Students;
using FocusLens.Domain.StudySessions;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Progress;

public sealed class BehavioralProgressQueryHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    IBaseRepository<StudySession> sessionRepository,
    IBaseRepository<StudySessionBehaviorWindow> windowRepository,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IStudentLocalTime? studentLocalTime = null)
    : IRequestHandler<GetBehavioralProgressQuery, BehavioralProgressResponse?>
{
    private const int EnoughDataDayCount = 3;

    public async Task<BehavioralProgressResponse?> Handle(
        GetBehavioralProgressQuery request, CancellationToken cancellationToken)
    {
        Student? student = await GetAuthorizedStudentAsync(request.StudentId);
        if (student is null)
        {
            return null;
        }

        IStudentLocalTime localTime = studentLocalTime ??= new StudentLocalTime(timeProvider);
        DateOnly today = localTime.GetToday(student);
        string range = string.IsNullOrWhiteSpace(request.Range) ? "Last30Days" : request.Range.Trim();
        if (!IsValidRange(range, request.DateFrom, request.DateTo))
        {
            return null;
        }

        StudySession[] sessions = (await sessionRepository.GetAllAsync(session =>
                session.StudentId == student.Id && session.StartedAtUtc != null &&
                session.Status != StudySessionStatus.Active &&
                session.Status != StudySessionStatus.Draft &&
                session.Status != StudySessionStatus.Ready))
            .ToArray();
        Guid[] sessionIds = sessions.Select(session => session.Id).ToArray();
        StudySessionBehaviorWindow[] windows = sessionIds.Length == 0
            ? []
            : (await windowRepository.GetAllAsync(item => sessionIds.Contains(item.StudySessionId)))
            .OrderBy(item => item.WindowEndUtc)
            .ToArray();

        DateOnly earliest = windows.Length == 0
            ? today
            : windows.Min(item => localTime.GetLocalDate(item.WindowEndUtc, student));
        DateRange? dateRangeResult = ResolveRange(range, request.DateFrom, request.DateTo, today, earliest);
        if (dateRangeResult is null)
        {
            return null;
        }

        DateRange dateRange = dateRangeResult;
        StudySession[] sessionsInRange = sessions.Where(session =>
        {
            DateOnly sessionDate = localTime.GetLocalDate(session.StartedAtUtc!.Value, student);
            return sessionDate >= dateRange.From && sessionDate <= dateRange.To;
        }).ToArray();
        HashSet<Guid> sessionIdsInRange = sessionsInRange.Select(session => session.Id).ToHashSet();

        // Daily and time-of-day insights use each window's own local date, even when
        // its session started before the selected date range.
        StudySessionBehaviorWindow[] windowsInRange = windows.Where(item =>
        {
            DateOnly date = localTime.GetLocalDate(item.WindowEndUtc, student);
            return date >= dateRange.From && date <= dateRange.To;
        }).ToArray();

        // Subject insights remain scoped to sessions whose start date is in range.
        StudySessionBehaviorWindow[] subjectWindowsInRange = windowsInRange
            .Where(item => sessionIdsInRange.Contains(item.StudySessionId))
            .ToArray();

        BehavioralProgressDailyItemResponse[] daily = Enumerable
            .Range(0, dateRange.To.DayNumber - dateRange.From.DayNumber + 1)
            .Select(offset => dateRange.From.AddDays(offset))
            .Select(date => CreateDaily(date, windowsInRange, student, localTime))
            .ToArray();

        int analyzedDays = daily.Count(item => item.FocusScore.HasValue || item.UnderstandingScore.HasValue);
        ProgressDataStatus dataStatus = analyzedDays >= EnoughDataDayCount
            ? ProgressDataStatus.EnoughData
            : ProgressDataStatus.InsufficientData;
        BehavioralProgressSubjectItemResponse[] subjects = CreateSubjectInsights(
            sessionsInRange, subjectWindowsInRange, student, localTime);
        BehavioralProgressTimeOfDayItemResponse[] timeOfDay = CreateTimeOfDayInsights(
            windowsInRange, student, localTime);

        return new BehavioralProgressResponse(
            dateRange.From,
            dateRange.To,
            dataStatus,
            daily,
            CreateMetric(daily, item => item.FocusScore, dataStatus, "focus"),
            CreateMetric(daily, item => item.UnderstandingScore, dataStatus, "understanding"),
            subjects,
            timeOfDay,
            FindStrongestTimeOfDay(timeOfDay));
    }

    private static BehavioralProgressDailyItemResponse CreateDaily(
        DateOnly date,
        IReadOnlyCollection<StudySessionBehaviorWindow> windows,
        Student student,
        IStudentLocalTime localTime)
    {
        StudySessionBehaviorWindow[] dayWindows = windows.Where(item =>
            localTime.GetLocalDate(item.WindowEndUtc, student) == date).ToArray();
        return new BehavioralProgressDailyItemResponse(
            date,
            dayWindows.Length,
            AverageFocusScore(dayWindows),
            Average(dayWindows.Select(item => item.UnderstandingScore)));
    }

    private static int? AverageFocusScore(IEnumerable<StudySessionBehaviorWindow> windows)
    {
        StudySessionBehaviorWindow[] scoredWindows = windows
            .Where(window => window.FocusScore.HasValue)
            .ToArray();
        if (scoredWindows.Length == 0)
        {
            return null;
        }

        // Legacy windows have no active-time measurement. Avoid mixing weighted and
        // unweighted observations within a day; preserve the arithmetic mean for that day.
        if (scoredWindows.Any(window => !window.WindowActiveTimeSeconds.HasValue))
        {
            return Average(scoredWindows.Select(window => window.FocusScore));
        }

        StudySessionBehaviorWindow[] weightedWindows = scoredWindows
            .Where(window => window.WindowActiveTimeSeconds is double seconds &&
                             double.IsFinite(seconds) && seconds > 0)
            .ToArray();
        if (weightedWindows.Length == 0)
        {
            return null;
        }

        double totalWeight = weightedWindows.Sum(window => window.WindowActiveTimeSeconds!.Value);
        double weightedScore = weightedWindows.Sum(window =>
            window.FocusScore!.Value * window.WindowActiveTimeSeconds!.Value);
        return (int)Math.Round(weightedScore / totalWeight, MidpointRounding.AwayFromZero);
    }

    private static BehavioralProgressSubjectItemResponse[] CreateSubjectInsights(
        IReadOnlyCollection<StudySession> sessions,
        IReadOnlyCollection<StudySessionBehaviorWindow> windows,
        Student student,
        IStudentLocalTime localTime) =>
        sessions.GroupBy(session => session.SelectedSubjectId)
            .Select(group =>
            {
                HashSet<Guid> subjectSessionIds = group.Select(session => session.Id).ToHashSet();
                StudySessionBehaviorWindow[] subjectWindows = windows
                    .Where(window => subjectSessionIds.Contains(window.StudySessionId) && window.FocusScore.HasValue)
                    .ToArray();
                DailyFocusScore[] dailyScores = GetDailyFocusScores(subjectWindows, student, localTime);
                ProgressDataStatus dataStatus = ToAnalyzedDayStatus(dailyScores.Length);

                return new BehavioralProgressSubjectItemResponse(
                    group.Key,
                    GetSubjectName(student, group.Key),
                    dailyScores.Length,
                    dailyScores.Length == 0 ? null : dailyScores[^1].Score,
                    CalculateFocusTrend(dailyScores),
                    dataStatus);
            })
            .OrderByDescending(item => item.AnalyzedDayCount)
            .ThenBy(item => item.Subject)
            .ToArray();

    private static BehavioralProgressTimeOfDayItemResponse[] CreateTimeOfDayInsights(
        IReadOnlyCollection<StudySessionBehaviorWindow> windows,
        Student student,
        IStudentLocalTime localTime)
    {
        string[] periods = ["Morning", "Afternoon", "Evening"];
        return periods.Select(period =>
        {
            StudySessionBehaviorWindow[] periodWindows = windows
                .Where(window => window.FocusScore.HasValue &&
                                 GetTimeOfDayPeriod(window.WindowEndUtc, student, localTime) == period)
                .ToArray();
            DailyFocusScore[] dailyScores = GetDailyFocusScores(periodWindows, student, localTime);
            int? averageScore = dailyScores.Length == 0
                ? null
                : (int)Math.Round(dailyScores.Average(item => item.Score), MidpointRounding.AwayFromZero);

            return new BehavioralProgressTimeOfDayItemResponse(
                period,
                periodWindows.Length,
                dailyScores.Length,
                averageScore,
                ToAnalyzedDayStatus(dailyScores.Length));
        }).ToArray();
    }

    // Report a strongest period only when at least two periods have enough data and differ by 5+ points.
    private static string? FindStrongestTimeOfDay(
        IReadOnlyCollection<BehavioralProgressTimeOfDayItemResponse> timeOfDay)
    {
        BehavioralProgressTimeOfDayItemResponse[] comparable = timeOfDay
            .Where(item => item.FocusDataStatus == ProgressDataStatus.EnoughData &&
                           item.AverageFocusScore.HasValue)
            .OrderByDescending(item => item.AverageFocusScore)
            .ToArray();

        if (comparable.Length < 2 ||
            comparable[0].AverageFocusScore!.Value - comparable[1].AverageFocusScore!.Value < 5)
        {
            return null;
        }

        return comparable[0].Period;
    }

    private static DailyFocusScore[] GetDailyFocusScores(
        IEnumerable<StudySessionBehaviorWindow> windows,
        Student student,
        IStudentLocalTime localTime) =>
        windows.Where(window => window.FocusScore.HasValue)
            .GroupBy(window => localTime.GetLocalDate(window.WindowEndUtc, student))
            .OrderBy(group => group.Key)
            .Select(group => new
            {
                Date = group.Key,
                Score = AverageFocusScore(group)
            })
            .Where(item => item.Score.HasValue)
            .Select(item => new DailyFocusScore(item.Date, item.Score!.Value))
            .ToArray();

    private static string? CalculateFocusTrend(IReadOnlyCollection<DailyFocusScore> dailyScores)
    {
        if (dailyScores.Count < EnoughDataDayCount)
        {
            return null;
        }

        int change = dailyScores.Last().Score - dailyScores.First().Score;
        return change >= 5 ? "Improving" : change <= -5 ? "Declining" : "Steady";
    }

    private static ProgressDataStatus ToAnalyzedDayStatus(int analyzedDayCount) =>
        analyzedDayCount >= EnoughDataDayCount
            ? ProgressDataStatus.EnoughData
            : ProgressDataStatus.InsufficientData;

    // Periods are evaluated in student-local time: Morning 05:00-11:59, Afternoon 12:00-16:59, Evening 17:00-04:59.
    private static string GetTimeOfDayPeriod(
        DateTimeOffset utcInstant,
        Student student,
        IStudentLocalTime localTime)
    {
        int hour = localTime.ConvertFromUtc(utcInstant, student).Hour;
        return hour switch
        {
            >= 5 and < 12 => "Morning",
            >= 12 and < 17 => "Afternoon",
            _ => "Evening"
        };
    }

    private static string GetSubjectName(Student student, Guid? subjectId)
    {
        StudentSubject? subject = student.Subjects.FirstOrDefault(item => item.Id == subjectId);
        if (subject is null)
        {
            return "Unknown subject";
        }

        return subject.Type == StudentSubjectType.Other
            ? subject.CustomName ?? "Other"
            : subject.Type.ToString();
    }

    private sealed record DailyFocusScore(DateOnly Date, int Score);

    private static BehavioralProgressMetricResponse CreateMetric(
        IReadOnlyCollection<BehavioralProgressDailyItemResponse> daily,
        Func<BehavioralProgressDailyItemResponse, int?> selector,
        ProgressDataStatus dataStatus,
        string metric)
    {
        int[] scores = daily.Select(selector).Where(score => score.HasValue).Select(score => score!.Value).ToArray();
        int? latest = scores.Length == 0 ? null : scores[^1];
        string calculation = metric == "focus"
            ? "Windows are grouped by the student's configured local calendar date. Daily focus scores are weighted by active time; legacy days containing scored windows without active-time data use a simple average."
            : "Windows are grouped by the student's configured local calendar date. Daily understanding scores are the arithmetic mean of scored analysis windows.";
        string interpretation = metric == "focus"
            ? "Higher values indicate more sustained study focus in analyzed windows."
            : "Higher values indicate stronger performance on analyzed learning signals.";
        if (dataStatus == ProgressDataStatus.InsufficientData || scores.Length < EnoughDataDayCount)
        {
            return new BehavioralProgressMetricResponse(latest, null,
                "More analyzed study days are needed before a trend is shown.", calculation, interpretation);
        }

        int change = scores[^1] - scores[0];
        string trend = change >= 5 ? "Improving" : change <= -5 ? "Declining" : "Steady";
        string insight = metric == "focus"
            ? $"Focus is {trend.ToLowerInvariant()} across the selected period."
            : $"Understanding is {trend.ToLowerInvariant()} across the selected period.";
        return new BehavioralProgressMetricResponse(latest, trend, insight, calculation, interpretation);
    }

    private static int? Average(IEnumerable<int?> values)
    {
        int[] scores = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return scores.Length == 0 ? null : (int)Math.Round(scores.Average(), MidpointRounding.AwayFromZero);
    }

    private static bool IsValidRange(string range, DateOnly? dateFrom, DateOnly? dateTo)
    {
        string normalized = range.ToLowerInvariant();
        return normalized is "last7days" or "week" or "last30days" or "month" or "alltime" ||
               (normalized == "custom" && dateFrom is not null && dateTo is not null && dateFrom <= dateTo);
    }

    private static DateRange? ResolveRange(
        string range,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        DateOnly today,
        DateOnly earliest) =>
        range.ToLowerInvariant() switch
        {
            "last7days" or "week" => new DateRange(today.AddDays(-6), today),
            "last30days" or "month" => new DateRange(today.AddDays(-29), today),
            "custom" when dateFrom is not null && dateTo is not null && dateFrom <= dateTo =>
                new DateRange(dateFrom.Value, dateTo.Value),
            "alltime" => new DateRange(earliest, today),
            _ => null
        };

    private async Task<Student?> GetAuthorizedStudentAsync(Guid? requestedStudentId)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return null;
        }

        Student? ownStudent = await studentRepository.FirstOrDefaultAsync(
            student => student.UserId == userId,
            student => student.Subjects);
        if (ownStudent is not null)
        {
            return requestedStudentId is null || requestedStudentId == ownStudent.Id ? ownStudent : null;
        }

        if (requestedStudentId is not Guid studentId || studentId == Guid.Empty)
        {
            return null;
        }

        Parent? parent = await parentRepository.FirstOrDefaultAsync(parent => parent.UserId == userId);
        if (parent is null || await relationshipRepository.FirstOrDefaultAsync(item =>
                item.ParentId == parent.Id && item.StudentId == studentId &&
                item.Status == RelationshipStatus.Active) is null)
        {
            return null;
        }

        Student? connectedStudent = await studentRepository.FirstOrDefaultAsync(
            student => student.Id == studentId,
            student => student.Subjects);

        return connectedStudent?.ShareSubjectTrendsWithParents == true
            ? connectedStudent
            : null;
    }

    private sealed record DateRange(DateOnly From, DateOnly To);
}
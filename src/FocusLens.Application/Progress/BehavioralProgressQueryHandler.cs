using FocusLens.Contracts.Progress;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.StudySessions;
using FocusLens.Domain.Students;
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
    TimeProvider timeProvider)
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

        DateOnly today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        string range = string.IsNullOrWhiteSpace(request.Range) ? "Month" : request.Range.Trim();
        if (!IsValidRange(range))
        {
            return null;
        }

        StudySession[] sessions = (await sessionRepository.GetAllAsync(session =>
                session.StudentId == student.Id && session.StartedAtUtc != null &&
                session.Status != StudySessionStatus.Draft && session.Status != StudySessionStatus.Ready))
            .ToArray();
        Guid[] sessionIds = sessions.Select(session => session.Id).ToArray();
        StudySessionBehaviorWindow[] windows = sessionIds.Length == 0
            ? []
            : (await windowRepository.GetAllAsync(item => sessionIds.Contains(item.StudySessionId)))
                .OrderBy(item => item.WindowEndUtc)
                .ToArray();

        DateOnly earliest = windows.Length == 0
            ? today
            : DateOnly.FromDateTime(windows.Min(item => item.WindowEndUtc).UtcDateTime);
        DateRange dateRange = ResolveRange(range, today, earliest);
        StudySessionBehaviorWindow[] included = windows.Where(item =>
        {
            DateOnly date = DateOnly.FromDateTime(item.WindowEndUtc.UtcDateTime);
            return date >= dateRange.From && date <= dateRange.To;
        }).ToArray();

        BehavioralProgressDailyItemResponse[] daily = Enumerable
            .Range(0, dateRange.To.DayNumber - dateRange.From.DayNumber + 1)
            .Select(offset => dateRange.From.AddDays(offset))
            .Select(date => CreateDaily(date, included))
            .ToArray();

        int analyzedDays = daily.Count(item => item.FocusScore.HasValue || item.UnderstandingScore.HasValue);
        ProgressDataStatus dataStatus = analyzedDays >= EnoughDataDayCount
            ? ProgressDataStatus.EnoughData
            : ProgressDataStatus.InsufficientData;
        return new BehavioralProgressResponse(
            dateRange.From,
            dateRange.To,
            dataStatus,
            daily,
            CreateMetric(daily, item => item.FocusScore, dataStatus, "focus"),
            CreateMetric(daily, item => item.UnderstandingScore, dataStatus, "understanding"));
    }

    private static BehavioralProgressDailyItemResponse CreateDaily(
        DateOnly date, IReadOnlyCollection<StudySessionBehaviorWindow> windows)
    {
        StudySessionBehaviorWindow[] dayWindows = windows.Where(item =>
            DateOnly.FromDateTime(item.WindowEndUtc.UtcDateTime) == date).ToArray();
        return new BehavioralProgressDailyItemResponse(
            date,
            dayWindows.Length,
            Average(dayWindows.Select(item => item.FocusScore)),
            Average(dayWindows.Select(item => item.UnderstandingScore)));
    }

    private static BehavioralProgressMetricResponse CreateMetric(
        IReadOnlyCollection<BehavioralProgressDailyItemResponse> daily,
        Func<BehavioralProgressDailyItemResponse, int?> selector,
        ProgressDataStatus dataStatus,
        string metric)
    {
        int[] scores = daily.Select(selector).Where(score => score.HasValue).Select(score => score!.Value).ToArray();
        int? latest = scores.Length == 0 ? null : scores[^1];
        const string calculation = "Each day is the average of persisted AI2 analysis windows for that calendar day (UTC).";
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

    private static bool IsValidRange(string range) => range.Equals("Week", StringComparison.OrdinalIgnoreCase) ||
        range.Equals("Month", StringComparison.OrdinalIgnoreCase) ||
        range.Equals("AllTime", StringComparison.OrdinalIgnoreCase);

    private static DateRange ResolveRange(string range, DateOnly today, DateOnly earliest) =>
        range.ToLowerInvariant() switch
        {
            "week" => new DateRange(today.AddDays(-6), today),
            "month" => new DateRange(today.AddDays(-29), today),
            _ => new DateRange(earliest, today)
        };

    private async Task<Student?> GetAuthorizedStudentAsync(Guid? requestedStudentId)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
            return null;
        Student? ownStudent = await studentRepository.FirstOrDefaultAsync(student => student.UserId == userId);
        if (ownStudent is not null)
            return requestedStudentId is null || requestedStudentId == ownStudent.Id ? ownStudent : null;
        if (requestedStudentId is not Guid studentId || studentId == Guid.Empty)
            return null;
        Parent? parent = await parentRepository.FirstOrDefaultAsync(parent => parent.UserId == userId);
        if (parent is null || await relationshipRepository.FirstOrDefaultAsync(item =>
                item.ParentId == parent.Id && item.StudentId == studentId && item.Status == RelationshipStatus.Active) is null)
            return null;
        return await studentRepository.FirstOrDefaultAsync(student => student.Id == studentId);
    }

    private sealed record DateRange(DateOnly From, DateOnly To);
}

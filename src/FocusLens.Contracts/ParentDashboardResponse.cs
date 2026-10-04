namespace FocusLens.Contracts;

public sealed record ParentDashboardResponse(
    Guid StudentId,
    ParentDashboardWeekResponse Week,
    ParentDashboardWeeklyStudyPulseResponse WeeklyStudyPulse,
    ParentDashboardFocusPatternResponse FocusPattern)
;

public sealed record ParentDashboardWeekResponse(DateOnly StartsOn, DateOnly EndsOn, bool IsCurrentWeek);

public sealed record ParentDashboardStudySessionResponse(
    Guid Id,
    string Status,
    string Mode,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    DateTimeOffset? LastActivityAtUtc,
    Guid? SelectedSubjectId,
    string? SubjectName,
    string ActualStudyMinutes,
    string? PlannedFocusDurationMinutes,
    int? FocusQuality,
    string? FocusState,
    string? FocusTrend
);

public sealed record ParentDashboardWeeklyStudyPulseResponse(
    string TotalStudyMinutes,
    string PreviousWeekStudyMinutes,
    string StudyMinutesTrend,
    IReadOnlyCollection<ParentDashboardWeeklyStudyPulseDayResponse> Days,
    ParentDashboardWeeklyStudyPulseDayResponse? PeakDay,
    int TotalSessions,
    int CompletedSessions,
    int ActiveStudyDays,
    ParentDashboardFocusQualityResponse FocusQuality
);

public sealed record ParentDashboardFocusQualityResponse(string? Trend);

public sealed record ParentOverviewWeekResponse(DateOnly StartsOn, DateOnly EndsOn, bool IsCurrentWeek);

public sealed record ParentOverviewWeeksResponse(IReadOnlyCollection<ParentOverviewWeekResponse> Weeks);

public sealed record UpdateParentOverviewWeekRequest(DateOnly? WeekStart);

public sealed record ParentDashboardWeeklyStudyPulseDayResponse(
    DateOnly Date,
    string Day,
    string StudyMinutes
);

public sealed record ParentDashboardFocusPatternResponse(
    IReadOnlyCollection<ParentDashboardFocusPatternDayResponse> Days,
    ParentDashboardFocusPatternTimeBlockResponse? MostActiveTimeBlock
);

public sealed record ParentDashboardFocusPatternDayResponse(
    string Day,
    IReadOnlyCollection<ParentDashboardFocusPatternSessionResponse> Sessions
);

public sealed record ParentDashboardFocusPatternSessionResponse(
    string StartTime,
    string StudyMinutes
);

public sealed record ParentDashboardFocusPatternTimeBlockResponse(
    string StartTime,
    string EndTime
);

public sealed record ParentDashboardSessionHistoryResponse(
    IReadOnlyCollection<ParentDashboardStudySessionResponse> Sessions,
    ParentDashboardPaginationResponse Pagination
)
{
    public ParentDashboardWeekResponse? Week { get; init; }
}

public sealed record ParentDashboardPaginationResponse(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages
);

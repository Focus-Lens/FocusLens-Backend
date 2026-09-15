using FocusLens.Contracts.Students;

namespace FocusLens.Contracts;

public sealed record ParentDashboardResponse(
    Guid StudentId,
    StudyTimeGoalResponse? CurrentStudyGoal,
    ParentDashboardStudyGoalProgressResponse? CurrentStudyGoalProgress,
    StudyGoalProposalResponse? PendingStudyGoalProposal,
    StudyGoalProposalResponse? CurrentStudyGoalAcceptedProposal,
    IReadOnlyCollection<ParentDashboardStudySessionResponse> RecentStudySessions,
    int CompletedSessionsCount,
    int ActiveStudyDaysCount,
    int? FocusQuality,
    ParentDashboardWeeklyStudyPulseResponse WeeklyStudyPulse
);

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
    int ActualStudyMinutes,
    int? PlannedFocusDurationMinutes
);

public sealed record ParentDashboardWeeklyStudyPulseResponse(
    DayOfWeek WeekStartsOn,
    DateOnly StartsOn,
    DateOnly EndsOn,
    int ActualStudyMinutes,
    int PreviousPeriodActualStudyMinutes,
    int ActualStudyMinutesTrend,
    IReadOnlyCollection<ParentDashboardWeeklyStudyPulseDayResponse> Days
);

public sealed record ParentDashboardWeeklyStudyPulseDayResponse(
    DateOnly Date,
    int ActualStudyMinutes
);

public sealed record ParentDashboardStudyGoalProgressResponse(
    int CompletedMinutes,
    decimal CompletionPercentage,
    int DaysRemaining,
    DateOnly StartsOn,
    DateOnly EndsOn,
    IReadOnlyCollection<ParentDashboardWeeklyStudyPulseDayResponse> Days
);

public sealed record ParentDashboardSessionHistoryResponse(
    IReadOnlyCollection<ParentDashboardStudySessionResponse> Sessions,
    ParentDashboardPaginationResponse Pagination
);

public sealed record ParentDashboardPaginationResponse(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages
);

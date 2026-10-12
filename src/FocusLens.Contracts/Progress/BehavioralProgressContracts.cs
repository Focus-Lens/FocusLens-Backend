namespace FocusLens.Contracts.Progress;

public sealed record BehavioralProgressDailyItemResponse(
    DateOnly Date,
    int AnalyzedWindowCount,
    int? FocusScore,
    int? UnderstandingScore);

public sealed record BehavioralProgressMetricResponse(
    int? LatestScore,
    string? Trend,
    string? Insight,
    string Calculation,
    string Interpretation);

public sealed record BehavioralProgressSubjectItemResponse(
    Guid? SubjectId,
    string Subject,
    int AnalyzedDayCount,
    int? LatestFocusScore,
    string? FocusTrend,
    ProgressDataStatus FocusDataStatus);

public sealed record BehavioralProgressTimeOfDayItemResponse(
    string Period,
    int AnalyzedWindowCount,
    int AnalyzedDayCount,
    int? AverageFocusScore,
    ProgressDataStatus FocusDataStatus);

public sealed record BehavioralProgressResponse(
    DateOnly DateFrom,
    DateOnly DateTo,
    ProgressDataStatus DataStatus,
    IReadOnlyCollection<BehavioralProgressDailyItemResponse> Daily,
    BehavioralProgressMetricResponse Focus,
    BehavioralProgressMetricResponse Understanding,
    IReadOnlyCollection<BehavioralProgressSubjectItemResponse> Subjects,
    IReadOnlyCollection<BehavioralProgressTimeOfDayItemResponse> TimeOfDay,
    string? StrongestTimeOfDay);
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

public sealed record BehavioralProgressResponse(
    DateOnly DateFrom,
    DateOnly DateTo,
    ProgressDataStatus DataStatus,
    IReadOnlyCollection<BehavioralProgressDailyItemResponse> Daily,
    BehavioralProgressMetricResponse Focus,
    BehavioralProgressMetricResponse Understanding);
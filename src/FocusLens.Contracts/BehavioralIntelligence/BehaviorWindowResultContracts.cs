namespace FocusLens.Contracts.BehavioralIntelligence;

public sealed record BehaviorWindowResultResponse(
    int WindowIndex,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    bool IsFinal,
    int? FocusScore,
    string? FocusState,
    string? FocusTrend,
    int? UnderstandingScore,
    string? UnderstandingTrend,
    string? RawAction,
    string? RecommendedAction,
    bool? ActionEmitted)
{
    public double? WindowActiveTimeSeconds { get; init; }
}
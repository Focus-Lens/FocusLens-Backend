namespace FocusLens.Contracts.BehavioralIntelligence;

public sealed record BehaviorWindowRequest(
    string UserId,
    string SessionId,
    int WindowIndex,
    long WindowStart,
    long WindowEnd,
    bool IsFinal,
    IReadOnlyCollection<BehaviorSection> Sections,
    IReadOnlyCollection<BehaviorWindowHistoryItem> History);

public sealed record BehaviorSection(
    string SectionId,
    string ConceptId,
    long SectionStartTime,
    long SectionEndTime,
    double TimeSpentSeconds,
    double ScrollSpeedAvgPxPerSec,
    int ScrollDirectionChanges,
    double ContentProgressionPct,
    int SectionRevisitCount,
    int InteractionCount,
    IReadOnlyCollection<BehaviorMicroChallenge> MicroChallenges,
    int BackgroundCount,
    double TotalBackgroundSeconds,
    int TabHiddenCount);

public sealed record BehaviorMicroChallenge(
    string QuestionId,
    double ResponseTimeSeconds,
    bool IsCorrect);

public sealed record BehaviorWindowHistoryItem(
    int WindowIndex,
    int FocusScore,
    string State,
    string DominantAction,
    int? UnderstandingScore);

public sealed record BehaviorWindowResponse(
    string SessionId,
    int WindowIndex,
    int? WindowFocusScore,
    int? WindowUnderstandingScore,
    string? UnderstandingTrend,
    string WindowState,
    string RecommendedAction,
    string RawAction,
    bool ActionEmitted,
    string Trend,
    bool IsFinal,
    int SectionsAnalyzed,
    IReadOnlyCollection<BehaviorSectionResult> Sections);

public sealed record BehaviorSectionResult(
    string SectionId,
    string ConceptId,
    string State,
    double Confidence,
    int FocusScore,
    string RecommendedAction,
    IReadOnlyDictionary<string, object?> FeaturesUsed,
    bool McqDataAvailable);

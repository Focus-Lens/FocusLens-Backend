using System.Text.Json.Serialization;

namespace FocusLens.Contracts.Reports;

public sealed record ReportSessionListItemResponse(
    Guid SessionId,
    DateTimeOffset StartedAtUtc,
    string Subject,
    string MaterialName,
    string Mode,
    string SessionType,
    string Status,
    int ActualDurationMinutes,
    int CompletionPercentage,
    int QuestionsGenerated,
    int QuestionsAnswered,
    int CorrectQuestions,
    int Attempts,
    int LearningPercentage,
    int? FocusScore,
    string? FocusState,
    string? WithinSessionFocusTrend,
    int? UnderstandingScore,
    string? UnderstandingTrend)
{
    [JsonPropertyName("focusTrend")]
    public string? FocusTrend => WithinSessionFocusTrend;
}

public sealed record ReportSessionListResponse(
    IReadOnlyCollection<ReportSessionListItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record ReportSessionFocusQualityPointResponse(
    int WindowIndex,
    DateTimeOffset WindowStartLocal,
    DateTimeOffset WindowEndLocal,
    int? FocusScore);

public sealed record ReportSessionDetailResponse(
    Guid SessionId,
    DateTimeOffset StartedAtUtc,
    string Subject,
    string MaterialName,
    string Mode,
    string SessionType,
    string Status,
    int ActualDurationMinutes,
    int CompletionPercentage,
    int QuestionsGenerated,
    int QuestionsAnswered,
    int CorrectQuestions,
    int Attempts,
    int LearningPercentage,
    int CompletedSections,
    int TotalSelectedSections,
    int PauseCount,
    int? FocusScore,
    string? FocusState,
    string? WithinSessionFocusTrend,
    string? RecentSessionsFocusTrend,
    IReadOnlyCollection<ReportSessionFocusQualityPointResponse> FocusQuality,
    int? UnderstandingScore,
    string? UnderstandingTrend,
    string Summary,
    IReadOnlyCollection<string> Highlights)
{
    [JsonPropertyName("focusTrend")]
    public string? FocusTrend => WithinSessionFocusTrend;
}

namespace FocusLens.Contracts.Progress;

public enum ProgressDataStatus
{
    InsufficientData,
    EnoughData
}

public sealed record ProgressDailyItemResponse(
    DateOnly Date,
    int SessionCount,
    int ActualStudyMinutes);

public sealed record ProgressSubjectItemResponse(
    Guid? SubjectId,
    string Subject,
    int SessionCount,
    int ActualStudyMinutes,
    int CompletionPercentage,
    int LearningPercentage,
    ProgressDataStatus DataStatus);

public sealed record ProgressResponse(
    DateOnly DateFrom,
    DateOnly DateTo,
    int SessionCount,
    int ActualStudyMinutes,
    int LearningPercentage,
    ProgressDataStatus DataStatus,
    IReadOnlyCollection<ProgressDailyItemResponse> Daily,
    IReadOnlyCollection<ProgressSubjectItemResponse> Subjects);
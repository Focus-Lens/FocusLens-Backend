namespace FocusLens.Contracts.StudySessions;

public sealed record SubmitStudySessionQuestionAnswerRequest(
    string SelectedAnswer);

public sealed record StudySessionCurrentQuestionResponse(
    bool ChallengeAvailable,
    StudySessionCurrentQuestionDetails? Question,
    bool RequiresExplanation = false);

public sealed record StudySessionCurrentQuestionDetails(
    Guid Id,
    int Number,
    int Total,
    string SectionTitle,
    string ConceptName,
    string Question,
    IReadOnlyCollection<string> Options,
    int EstimatedTimeMinutes);

public sealed record StudySessionQuestionAnswerResponse(
    Guid QuestionId,
    bool IsCorrect,
    string LearningSignal,
    string CorrectAnswer,
    string? Explanation,
    bool CanRetry,
    bool RequiresExplanation,
    int AttemptNumber,
    bool SessionCompleted,
    int? NextQuestionNumber,
    int TotalQuestions);

public sealed record StudySessionQuestionExplanationResponse(
    Guid QuestionId,
    string SectionTitle,
    string ConceptName,
    string Explanation);

public sealed record PostponeStudySessionQuestionResponse(
    Guid QuestionId,
    DateTimeOffset ChallengeDeferredUntilUtc);

public sealed record StudySessionResultResponse(
    Guid SessionId,
    string Status,
    int FocusedMinutes,
    int CompletedSections,
    int TotalSections,
    string MaterialFileName,
    int QuestionCount,
    int CorrectAnswers,
    int TotalAttempts,
    int StreakDays);

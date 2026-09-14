namespace FocusLens.Application.Common.Interfaces;

public interface IFocusLensAiClient
{
    Task<AiContentProcessingResult> ProcessFileAsync(
        byte[] content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken);

    Task<AiMcqGenerationResult> GenerateMcqAsync(
        string sectionId,
        string sectionTitle,
        string conceptId,
        string conceptName,
        string content,
        int numberOfQuestions,
        CancellationToken cancellationToken);

    Task<AiAnswerEvaluationResult> EvaluateAnswerAsync(
        string question,
        string selectedAnswer,
        string correctAnswer,
        string sectionId,
        string conceptId,
        string content,
        CancellationToken cancellationToken);

    Task<AiExplanationResult> GenerateExplanationAsync(
        string sectionId,
        string sectionTitle,
        string conceptId,
        string conceptName,
        string content,
        CancellationToken cancellationToken);
}

public sealed record AiContentProcessingResult(
    string Status,
    string InputType,
    string ExtractedText,
    AiContentAnalysisResult? Content);

public sealed record AiContentAnalysisResult(
    IReadOnlyCollection<AiContentSection> Sections);

public sealed record AiContentSection(
    string SectionId,
    string Title,
    IReadOnlyCollection<AiContentConcept> Concepts,
    string Difficulty,
    int EstimatedTimeMinutes,
    int FromPage,
    int ToPage);

public sealed record AiContentConcept(
    string ConceptId,
    string Name,
    IReadOnlyCollection<string> KeyLearningPoints,
    string Difficulty,
    int EstimatedTimeMinutes);

public sealed record AiMcqGenerationResult(
    IReadOnlyCollection<AiMcqQuestion> Questions);

public sealed record AiMcqQuestion(
    string Question,
    IReadOnlyCollection<string> Options,
    string CorrectAnswer,
    string SectionId,
    string SectionTitle,
    string ConceptId,
    string ConceptName,
    string Difficulty,
    string Explanation,
    int EstimatedTimeMinutes);

public sealed record AiAnswerEvaluationResult(
    bool IsCorrect,
    string SectionId,
    string ConceptId,
    string LearningSignal);

public sealed record AiExplanationResult(
    string SectionId,
    string SectionTitle,
    string ConceptId,
    string ConceptName,
    string Explanation);

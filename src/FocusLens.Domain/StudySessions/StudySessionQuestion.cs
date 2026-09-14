using FocusLens.Domain.Common;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Domain.StudySessions;

public sealed class StudySessionQuestion : Entity
{
    private readonly List<StudySessionQuestionOption> _options = [];

    private StudySessionQuestion()
    {
    }

    private StudySessionQuestion(
        Guid studySessionId,
        Guid? studyMaterialSectionId,
        string aiSectionId,
        string aiSectionTitle,
        string aiConceptId,
        string aiConceptName,
        string question,
        string correctAnswer,
        string difficulty,
        string explanation,
        int estimatedTimeMinutes,
        int order,
        IReadOnlyCollection<string> options)
        : base(Guid.CreateVersion7())
    {
        StudySessionId = studySessionId;
        StudyMaterialSectionId = studyMaterialSectionId;
        AiSectionId = aiSectionId;
        AiSectionTitle = aiSectionTitle;
        AiConceptId = aiConceptId;
        AiConceptName = aiConceptName;
        Question = question;
        CorrectAnswer = correctAnswer;
        Difficulty = difficulty;
        Explanation = explanation;
        EstimatedTimeMinutes = estimatedTimeMinutes;
        Order = order;

        for (int index = 0; index < options.Count; index++)
        {
            _options.Add(
                StudySessionQuestionOption.Create(
                    Id,
                    options.ElementAt(index),
                    index + 1));
        }
    }

    public Guid StudySessionId { get; private set; }

    public Guid? StudyMaterialSectionId { get; private set; }

    public string AiSectionId { get; private set; } = string.Empty;

    public string AiSectionTitle { get; private set; } = string.Empty;

    public string AiConceptId { get; private set; } = string.Empty;

    public string AiConceptName { get; private set; } = string.Empty;

    public string Question { get; private set; } = string.Empty;

    public string CorrectAnswer { get; private set; } = string.Empty;

    public string Difficulty { get; private set; } = string.Empty;

    public string Explanation { get; private set; } = string.Empty;

    public DateTimeOffset? ExplanationShownAtUtc { get; private set; }

    public int EstimatedTimeMinutes { get; private set; }

    public int Order { get; private set; }

    public IReadOnlyCollection<StudySessionQuestionOption> Options =>
        _options.AsReadOnly();

    public Result<Success> MarkExplanationShown(DateTimeOffset shownAtUtc)
    {
        ExplanationShownAtUtc ??= shownAtUtc;
        return Result.Success;
    }

    public static Result<StudySessionQuestion> Create(
        Guid studySessionId,
        Guid? studyMaterialSectionId,
        string? aiSectionId,
        string? aiSectionTitle,
        string? aiConceptId,
        string? aiConceptName,
        string? question,
        IEnumerable<string>? options,
        string? correctAnswer,
        string? difficulty,
        string? explanation,
        int estimatedTimeMinutes,
        int order)
    {
        if (studySessionId == Guid.Empty)
        {
            return Error.Validation(
                "StudySessionQuestions.SessionIdRequired",
                "Study session id is required.");
        }

        if (string.IsNullOrWhiteSpace(aiSectionId))
        {
            return Error.Validation(
                "StudySessionQuestions.AiSectionIdRequired",
                "AI section id is required.");
        }

        if (string.IsNullOrWhiteSpace(aiSectionTitle))
        {
            return Error.Validation(
                "StudySessionQuestions.AiSectionTitleRequired",
                "AI section title is required.");
        }

        if (string.IsNullOrWhiteSpace(aiConceptId))
        {
            return Error.Validation(
                "StudySessionQuestions.AiConceptIdRequired",
                "AI concept id is required.");
        }

        if (string.IsNullOrWhiteSpace(aiConceptName))
        {
            return Error.Validation(
                "StudySessionQuestions.AiConceptNameRequired",
                "AI concept name is required.");
        }

        if (string.IsNullOrWhiteSpace(question))
        {
            return Error.Validation(
                "StudySessionQuestions.QuestionRequired",
                "Question is required.");
        }

        string[] optionValues = options?
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => option.Trim())
            .ToArray()
            ?? [];

        if (optionValues.Length != 4)
        {
            return Error.Validation(
                "StudySessionQuestions.OptionsInvalid",
                "A question must contain exactly four options.");
        }

        if (optionValues.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4)
        {
            return Error.Validation(
                "StudySessionQuestions.OptionsDuplicate",
                "Question options must be unique.");
        }

        string normalizedCorrectAnswer = correctAnswer?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedCorrectAnswer) ||
            !optionValues.Any(option =>
                string.Equals(
                    option,
                    normalizedCorrectAnswer,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Validation(
                "StudySessionQuestions.CorrectAnswerInvalid",
                "The correct answer must match one of the question options.");
        }

        string normalizedDifficulty =
            difficulty?.Trim().ToLowerInvariant() ?? string.Empty;

        if (normalizedDifficulty is not ("easy" or "medium" or "hard"))
        {
            return Error.Validation(
                "StudySessionQuestions.DifficultyInvalid",
                "Question difficulty must be easy, medium, or hard.");
        }

        if (string.IsNullOrWhiteSpace(explanation))
        {
            return Error.Validation(
                "StudySessionQuestions.ExplanationRequired",
                "Question explanation is required.");
        }

        if (estimatedTimeMinutes <= 0)
        {
            return Error.Validation(
                "StudySessionQuestions.EstimatedTimeInvalid",
                "Question estimated time must be greater than zero.");
        }

        if (order <= 0)
        {
            return Error.Validation(
                "StudySessionQuestions.OrderInvalid",
                "Question order must be greater than zero.");
        }

        return new StudySessionQuestion(
            studySessionId,
            studyMaterialSectionId,
            aiSectionId.Trim(),
            aiSectionTitle.Trim(),
            aiConceptId.Trim(),
            aiConceptName.Trim(),
            question.Trim(),
            normalizedCorrectAnswer,
            normalizedDifficulty,
            explanation.Trim(),
            estimatedTimeMinutes,
            order,
            optionValues);
    }
}

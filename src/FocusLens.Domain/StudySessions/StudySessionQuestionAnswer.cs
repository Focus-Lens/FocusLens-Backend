using FocusLens.Domain.Common;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Domain.StudySessions;

public sealed class StudySessionQuestionAnswer : Entity
{
    private StudySessionQuestionAnswer()
    {
    }

    private StudySessionQuestionAnswer(
        Guid questionId,
        string selectedAnswer,
        bool isCorrect,
        string? learningSignal,
        int attemptNumber,
        DateTimeOffset answeredAtUtc)
        : base(Guid.CreateVersion7())
    {
        StudySessionQuestionId = questionId;
        SelectedAnswer = selectedAnswer;
        IsCorrect = isCorrect;
        LearningSignal = learningSignal;
        AttemptNumber = attemptNumber;
        AnsweredAtUtc = answeredAtUtc;
    }

    public Guid StudySessionQuestionId { get; private set; }

    public string SelectedAnswer { get; private set; } = string.Empty;

    public bool IsCorrect { get; private set; }

    public string? LearningSignal { get; private set; }

    public int AttemptNumber { get; private set; }

    public DateTimeOffset AnsweredAtUtc { get; private set; }

    public static Result<StudySessionQuestionAnswer> Create(
        Guid questionId,
        string? selectedAnswer,
        bool isCorrect,
        string? learningSignal,
        int attemptNumber,
        DateTimeOffset answeredAtUtc)
    {
        if (questionId == Guid.Empty)
        {
            return Error.Validation(
                "StudySessionQuestionAnswers.QuestionIdRequired",
                "Question id is required.");
        }

        if (string.IsNullOrWhiteSpace(selectedAnswer))
        {
            return Error.Validation(
                "StudySessionQuestionAnswers.SelectedAnswerRequired",
                "Selected answer is required.");
        }

        if (attemptNumber <= 0)
        {
            return Error.Validation(
                "StudySessionQuestionAnswers.AttemptInvalid",
                "Attempt number must be greater than zero.");
        }

        return new StudySessionQuestionAnswer(
            questionId,
            selectedAnswer.Trim(),
            isCorrect,
            string.IsNullOrWhiteSpace(learningSignal) ? null : learningSignal.Trim(),
            attemptNumber,
            answeredAtUtc);
    }
}
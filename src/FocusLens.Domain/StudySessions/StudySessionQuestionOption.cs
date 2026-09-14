using FocusLens.Domain.Common;

namespace FocusLens.Domain.StudySessions;

public sealed class StudySessionQuestionOption : Entity
{
    private StudySessionQuestionOption()
    {
    }

    private StudySessionQuestionOption(
        Guid questionId,
        string text,
        int order)
        : base(Guid.CreateVersion7())
    {
        StudySessionQuestionId = questionId;
        Text = text;
        Order = order;
    }

    public Guid StudySessionQuestionId { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public int Order { get; private set; }

    public static StudySessionQuestionOption Create(
        Guid questionId,
        string text,
        int order)
    {
        return new StudySessionQuestionOption(
            questionId,
            text.Trim(),
            order);
    }
}

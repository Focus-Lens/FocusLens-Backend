namespace FocusLens.Domain.StudySessions;

public enum StudySessionBehaviorEventType
{
    Scroll,
    Interaction,
    AppBackground,
    AppForeground,
    TabHidden,
    TabVisible,
    QuestionShown,
    QuestionAnswered
}
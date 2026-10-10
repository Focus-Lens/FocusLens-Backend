namespace FocusLens.Contracts.StudySessions;

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

public sealed record RecordStudySessionBehaviorEventsRequest(
    IReadOnlyCollection<StudySessionBehaviorEventRequest>? Events);

public sealed record StudySessionBehaviorEventRequest(
    StudySessionBehaviorEventType EventType,
    DateTimeOffset OccurredAtUtc,
    Guid? StudyMaterialSectionId,
    Guid? StudySessionQuestionId,
    double? ScrollSpeedAvgPxPerSec,
    int? ScrollDirectionChanges,
    double? ContentProgressionPct,
    int? InteractionCount,
    int? BackgroundCount,
    double? BackgroundDurationSeconds);

public sealed record StudySessionBehaviorEventsResponse(int AcceptedCount);
using FocusLens.Domain.Common;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Domain.StudySessions;

public sealed class StudySessionBehaviorEvent : AuditableEntity
{
    private StudySessionBehaviorEvent() { }

    private StudySessionBehaviorEvent(
        Guid studySessionId,
        Guid? studyMaterialSectionId,
        Guid? studySessionQuestionId,
        StudySessionBehaviorEventType eventType,
        DateTimeOffset occurredAtUtc,
        double? scrollSpeedAvgPxPerSec,
        int? scrollDirectionChanges,
        double? contentProgressionPct,
        int? interactionCount,
        int? backgroundCount,
        double? backgroundDurationSeconds)
        : base(Guid.CreateVersion7())
    {
        StudySessionId = studySessionId;
        StudyMaterialSectionId = studyMaterialSectionId;
        StudySessionQuestionId = studySessionQuestionId;
        EventType = eventType;
        OccurredAtUtc = occurredAtUtc;
        ScrollSpeedAvgPxPerSec = scrollSpeedAvgPxPerSec;
        ScrollDirectionChanges = scrollDirectionChanges;
        ContentProgressionPct = contentProgressionPct;
        InteractionCount = interactionCount;
        BackgroundCount = backgroundCount;
        BackgroundDurationSeconds = backgroundDurationSeconds;
    }

    public Guid StudySessionId { get; private set; }
    public Guid? StudyMaterialSectionId { get; private set; }
    public Guid? StudySessionQuestionId { get; private set; }
    public StudySessionBehaviorEventType EventType { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public double? ScrollSpeedAvgPxPerSec { get; private set; }
    public int? ScrollDirectionChanges { get; private set; }
    public double? ContentProgressionPct { get; private set; }
    public int? InteractionCount { get; private set; }
    public int? BackgroundCount { get; private set; }
    public double? BackgroundDurationSeconds { get; private set; }

    public static Result<StudySessionBehaviorEvent> Create(
        Guid studySessionId,
        Guid? studyMaterialSectionId,
        Guid? studySessionQuestionId,
        StudySessionBehaviorEventType eventType,
        DateTimeOffset occurredAtUtc,
        double? scrollSpeedAvgPxPerSec,
        int? scrollDirectionChanges,
        double? contentProgressionPct,
        int? interactionCount,
        int? backgroundCount,
        double? backgroundDurationSeconds)
    {
        if (studySessionId == Guid.Empty)
        {
            return Error.Validation("StudySessionBehaviorEvents.SessionIdRequired", "Study session id is required.");
        }

        if (!Enum.IsDefined(eventType))
        {
            return Error.Validation("StudySessionBehaviorEvents.EventTypeInvalid", "Behavior event type is invalid.");
        }

        if (scrollSpeedAvgPxPerSec < 0 || scrollDirectionChanges < 0 ||
            contentProgressionPct is < 0 or > 100 || interactionCount < 0 ||
            backgroundCount < 0 || backgroundDurationSeconds < 0)
        {
            return Error.Validation("StudySessionBehaviorEvents.TelemetryInvalid",
                "Behavior telemetry values are invalid.");
        }

        return new StudySessionBehaviorEvent(
            studySessionId, studyMaterialSectionId, studySessionQuestionId, eventType, occurredAtUtc,
            scrollSpeedAvgPxPerSec, scrollDirectionChanges, contentProgressionPct, interactionCount,
            backgroundCount, backgroundDurationSeconds);
    }
}
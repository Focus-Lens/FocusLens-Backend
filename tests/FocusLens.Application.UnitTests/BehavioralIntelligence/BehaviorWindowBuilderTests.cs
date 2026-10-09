using FocusLens.Application.BehavioralIntelligence;
using FocusLens.Contracts.BehavioralIntelligence;
using FocusLens.Domain.StudySessions;


namespace FocusLens.Application.UnitTests.BehavioralIntelligence;

public sealed class BehaviorWindowBuilderTests
{
    [Fact]
    public void Build_UsesWindowEvents_AndSessionHistoryForSectionMetrics()
    {
        Guid studentId = Guid.NewGuid();
        Guid sessionId = Guid.NewGuid();
        Guid sectionA = Guid.NewGuid();
        Guid sectionB = Guid.NewGuid();
        DateTimeOffset start = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset middle = start.AddMinutes(1);
        DateTimeOffset end = start.AddMinutes(2);

        StudySessionBehaviorEvent[] events =
        [
            CreateEvent(sessionId, sectionA, null, StudySessionBehaviorEventType.Scroll, start, scrollSpeed: 100, directionChanges: 2, progression: 40),
            CreateEvent(sessionId, sectionB, null, StudySessionBehaviorEventType.Scroll, middle, scrollSpeed: 200, directionChanges: 1, progression: 60),
            CreateEvent(sessionId, sectionA, null, StudySessionBehaviorEventType.Interaction, end, interactionCount: 2, progression: 80)
        ];

        var builder = new BehaviorWindowBuilder();
        BehaviorWindowRequest result = builder.Build(
            studentId,
            sessionId,
            windowIndex: 1,
            start,
            end.AddTicks(1),
            isFinal: false,
            events,
            Array.Empty<StudySessionQuestion>(),
            Array.Empty<StudySessionQuestionAnswer>(),
            Array.Empty<BehaviorWindowHistoryItem>());

        Assert.Equal(studentId.ToString(), result.UserId);
        Assert.Equal(sessionId.ToString(), result.SessionId);
        Assert.Equal(1, result.WindowIndex);
        Assert.Equal(2, result.Sections.Count);

        BehaviorSection resultA = Assert.Single(result.Sections, x => x.SectionId == sectionA.ToString());
        Assert.Equal(100, resultA.ScrollSpeedAvgPxPerSec);
        Assert.Equal(2, resultA.ScrollDirectionChanges);
        Assert.Equal(80, resultA.ContentProgressionPct);
        Assert.Equal(1, resultA.SectionRevisitCount);
        Assert.Equal(2, resultA.InteractionCount);
    }

    [Fact]
    public void Build_UsesPersistedAnswer_AndPriorQuestionShown_ForMicroChallenge()
    {
        Guid sessionId = Guid.NewGuid();
        Guid sectionId = Guid.NewGuid();
        DateTimeOffset questionShownAt = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset answeredAt = questionShownAt.AddSeconds(12);

        StudySessionQuestion question = StudySessionQuestion.Create(
            sessionId,
            sectionId,
            "ai-section-1",
            "Section 1",
            "concept-1",
            "Concept 1",
            "What is 2 + 2?",
            ["2", "3", "4", "5"],
            "4",
            "easy",
            "Because 2 + 2 equals 4.",
            1,
            1).Value;

        StudySessionQuestionAnswer answer = StudySessionQuestionAnswer.Create(
            question.Id,
            "4",
            isCorrect: true,
            learningSignal: null,
            attemptNumber: 1,
            answeredAt).Value;

        StudySessionBehaviorEvent[] events =
        [
            CreateEvent(sessionId, sectionId, question.Id, StudySessionBehaviorEventType.QuestionShown, questionShownAt),
            CreateEvent(sessionId, sectionId, question.Id, StudySessionBehaviorEventType.QuestionAnswered, answeredAt)
        ];

        var builder = new BehaviorWindowBuilder();
        BehaviorWindowRequest result = builder.Build(
            Guid.NewGuid(),
            sessionId,
            0,
            questionShownAt,
            answeredAt.AddSeconds(1),
            isFinal: true,
            events,
            [question],
            [answer],
            Array.Empty<BehaviorWindowHistoryItem>());

        BehaviorSection section = Assert.Single(result.Sections);
        BehaviorMicroChallenge challenge = Assert.Single(section.MicroChallenges);
        Assert.Equal(question.Id.ToString(), challenge.QuestionId);
        Assert.Equal(12, challenge.ResponseTimeSeconds, 3);
        Assert.True(challenge.IsCorrect);
    }

    [Fact]
    public void Build_AttributesSectionlessBackgroundEventToActiveSection()
    {
        Guid sessionId = Guid.NewGuid();
        Guid sectionId = Guid.NewGuid();
        DateTimeOffset start = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);

        StudySessionBehaviorEvent[] events =
        [
            CreateEvent(
                sessionId,
                sectionId,
                null,
                StudySessionBehaviorEventType.Scroll,
                start.AddSeconds(10),
                scrollSpeed: 180,
                progression: 40),

            CreateEvent(
                sessionId,
                null,
                null,
                StudySessionBehaviorEventType.AppBackground,
                start.AddSeconds(20),
                backgroundCount: 1,
                backgroundSeconds: 8)
        ];

        var builder = new BehaviorWindowBuilder();

        BehaviorWindowRequest result = builder.Build(
            Guid.NewGuid(),
            sessionId,
            1,
            start,
            start.AddSeconds(30),
            isFinal: false,
            events,
            Array.Empty<StudySessionQuestion>(),
            Array.Empty<StudySessionQuestionAnswer>(),
            Array.Empty<BehaviorWindowHistoryItem>());

        BehaviorSection section = Assert.Single(result.Sections);

        Assert.Equal(sectionId.ToString(), section.SectionId);
        Assert.Equal(1, section.BackgroundCount);
        Assert.Equal(8, section.TotalBackgroundSeconds);
    }

    [Fact]
    public void Build_LeavesTabHiddenCountNull_WhenVisibilityTelemetryWasNotReported()
    {
        Guid sessionId = Guid.NewGuid();
        Guid sectionId = Guid.NewGuid();
        DateTimeOffset start = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);
        StudySessionBehaviorEvent[] events =
        [
            CreateEvent(sessionId, sectionId, null, StudySessionBehaviorEventType.Scroll, start),
            CreateEvent(sessionId, sectionId, null, StudySessionBehaviorEventType.Interaction, start.AddSeconds(10), interactionCount: 1)
        ];

        BehaviorWindowRequest request = new BehaviorWindowBuilder().Build(
            Guid.NewGuid(), sessionId, 1, start, start.AddSeconds(20), false,
            events, [], [], []);

        BehaviorSection section = Assert.Single(request.Sections);
        Assert.Null(section.TabHiddenCount);
        Assert.Null(section.ScrollSpeedAvgPxPerSec);
        Assert.Null(section.ScrollDirectionChanges);
        Assert.Null(section.BackgroundCount);
        Assert.Null(section.TotalBackgroundSeconds);
        Assert.Equal(1, section.InteractionCount);
    }

    [Fact]
    public void Build_PreservesMeasuredZeroForOptionalTelemetry()
    {
        Guid sessionId = Guid.NewGuid();
        Guid sectionId = Guid.NewGuid();
        DateTimeOffset start = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);
        StudySessionBehaviorEvent[] events =
        [
            CreateEvent(sessionId, sectionId, null, StudySessionBehaviorEventType.Scroll, start,
                scrollSpeed: 0, directionChanges: 0, progression: 0, interactionCount: 0,
                backgroundCount: 0, backgroundSeconds: 0)
        ];

        BehaviorWindowRequest request = new BehaviorWindowBuilder().Build(
            Guid.NewGuid(), sessionId, 1, start, start.AddSeconds(1), false, events, [], [], []);

        BehaviorSection section = Assert.Single(request.Sections);
        Assert.Equal(0, section.ScrollSpeedAvgPxPerSec);
        Assert.Equal(0, section.ScrollDirectionChanges);
        Assert.Equal(0, section.InteractionCount);
        Assert.Equal(0, section.BackgroundCount);
        Assert.Equal(0, section.TotalBackgroundSeconds);
    }

    [Fact]
    public void BuildReturnsZeroTabHiddenCount_WhenVisibilityTelemetryIsPresentButTabWasNotHidden()
    {
        Guid sessionId = Guid.NewGuid();
        Guid sectionId = Guid.NewGuid();
        DateTimeOffset start = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);
        StudySessionBehaviorEvent[] events =
        [
            CreateEvent(sessionId, sectionId, null, StudySessionBehaviorEventType.TabVisible, start),
            CreateEvent(sessionId, sectionId, null, StudySessionBehaviorEventType.Scroll, start.AddSeconds(10))
        ];

        BehaviorWindowRequest request = new BehaviorWindowBuilder().Build(
            Guid.NewGuid(), sessionId, 1, start, start.AddSeconds(20), false,
            events, [], [], []);

        Assert.Equal(0, Assert.Single(request.Sections).TabHiddenCount);
    }

    [Fact]
    public void Build_ExcludesEventAtWindowEnd()
    {
        Guid sessionId = Guid.NewGuid();
        Guid sectionId = Guid.NewGuid();
        DateTimeOffset start = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset end = start.AddMinutes(5);

        StudySessionBehaviorEvent[] events =
        [
            CreateEvent(sessionId, sectionId, null, StudySessionBehaviorEventType.Scroll, start.AddMinutes(1), scrollSpeed: 100),
            CreateEvent(sessionId, sectionId, null, StudySessionBehaviorEventType.Scroll, end, scrollSpeed: 500)
        ];

        var builder = new BehaviorWindowBuilder();
        BehaviorWindowRequest result = builder.Build(
            Guid.NewGuid(),
            sessionId,
            0,
            start,
            end,
            isFinal: false,
            events,
            Array.Empty<StudySessionQuestion>(),
            Array.Empty<StudySessionQuestionAnswer>(),
            Array.Empty<BehaviorWindowHistoryItem>());

        BehaviorSection section = Assert.Single(result.Sections);
        Assert.Equal(100, section.ScrollSpeedAvgPxPerSec);
    }

    private static StudySessionBehaviorEvent CreateEvent(
        Guid sessionId,
        Guid? sectionId,
        Guid? questionId,
        StudySessionBehaviorEventType eventType,
        DateTimeOffset occurredAt,
        double? scrollSpeed = null,
        int? directionChanges = null,
        double? progression = null,
        int? interactionCount = null,
        int? backgroundCount = null,
        double? backgroundSeconds = null)
    {
        return StudySessionBehaviorEvent.Create(
            sessionId,
            sectionId,
            questionId,
            eventType,
            occurredAt,
            scrollSpeed,
            directionChanges,
            progression,
            interactionCount,
            backgroundCount,
            backgroundSeconds).Value;
    }
}

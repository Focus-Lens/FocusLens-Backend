using FocusLens.Contracts.BehavioralIntelligence;
using FocusLens.Domain.StudySessions;

namespace FocusLens.Application.BehavioralIntelligence;

public sealed class BehaviorWindowBuilder
{
    public BehaviorWindowRequest Build(
        Guid studentId,
        Guid sessionId,
        int windowIndex,
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        bool isFinal,
        IReadOnlyCollection<StudySessionBehaviorEvent> sessionEventsThroughWindowEnd,
        IReadOnlyCollection<StudySessionQuestion> sessionQuestions,
        IReadOnlyCollection<StudySessionQuestionAnswer> sessionAnswers,
        IReadOnlyCollection<BehaviorWindowHistoryItem> history)
    {
        if (windowEndUtc < windowStartUtc)
        {
            throw new ArgumentException("Window end must not precede window start.");
        }

        StudySessionBehaviorEvent[] allEvents = sessionEventsThroughWindowEnd
            .Where(item => item.OccurredAtUtc <= windowEndUtc)
            .OrderBy(item => item.OccurredAtUtc)
            .ToArray();

        StudySessionBehaviorEvent[] windowEvents = allEvents
            .Where(item => item.OccurredAtUtc >= windowStartUtc && item.OccurredAtUtc < windowEndUtc)
            .ToArray();

        BehaviorSection[] sections = BuildSections(
            windowEvents,
            allEvents,
            windowStartUtc,
            windowEndUtc,
            sessionQuestions,
            sessionAnswers);

        return new BehaviorWindowRequest(
            UserId: studentId.ToString(),
            SessionId: sessionId.ToString(),
            WindowIndex: windowIndex,
            WindowStart: windowStartUtc.ToUnixTimeMilliseconds(),
            WindowEnd: windowEndUtc.ToUnixTimeMilliseconds(),
            IsFinal: isFinal,
            Sections: sections,
            History: history);
    }

    private static BehaviorSection[] BuildSections(
        IReadOnlyCollection<StudySessionBehaviorEvent> windowEvents,
        IReadOnlyCollection<StudySessionBehaviorEvent> allEvents,
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        IReadOnlyCollection<StudySessionQuestion> questions,
        IReadOnlyCollection<StudySessionQuestionAnswer> answers)
    {
        Dictionary<Guid, List<StudySessionBehaviorEvent>> sectionEvents = [];
        Guid? activeSection = FindActiveSectionBefore(windowStartUtc, allEvents);

        foreach (StudySessionBehaviorEvent item in allEvents.OrderBy(item => item.OccurredAtUtc))
        {
            if (item.OccurredAtUtc < windowStartUtc)
            {
                if (item.StudyMaterialSectionId.HasValue)
                {
                    activeSection = item.StudyMaterialSectionId.Value;
                }

                continue;
            }

            if (item.OccurredAtUtc >= windowEndUtc)
            {
                break;
            }

            if (item.StudyMaterialSectionId.HasValue)
            {
                activeSection = item.StudyMaterialSectionId.Value;
            }

            if (!activeSection.HasValue)
            {
                continue;
            }

            if (!sectionEvents.TryGetValue(
                    activeSection.Value,
                    out List<StudySessionBehaviorEvent>? eventsForSection))
            {
                eventsForSection = [];
                sectionEvents[activeSection.Value] = eventsForSection;
            }

            eventsForSection.Add(item);
        }

        return sectionEvents
            .Select(pair => BuildSection(
                pair.Key,
                pair.Value,
                allEvents,
                questions,
                answers))
            .ToArray();
    }

    private static Guid? FindActiveSectionBefore(
        DateTimeOffset windowStartUtc,
        IReadOnlyCollection<StudySessionBehaviorEvent> allEvents)
    {
        return allEvents
            .Where(item => item.OccurredAtUtc < windowStartUtc && item.StudyMaterialSectionId.HasValue)
            .OrderByDescending(item => item.OccurredAtUtc)
            .Select(item => item.StudyMaterialSectionId)
            .FirstOrDefault();
    }

    private static BehaviorSection BuildSection(
        Guid sectionId,
        IReadOnlyCollection<StudySessionBehaviorEvent> sectionEvents,
        IReadOnlyCollection<StudySessionBehaviorEvent> allEvents,
        IReadOnlyCollection<StudySessionQuestion> questions,
        IReadOnlyCollection<StudySessionQuestionAnswer> answers)
    {
        StudySessionBehaviorEvent[] orderedSectionEvents = sectionEvents
            .OrderBy(item => item.OccurredAtUtc)
            .ToArray();

        DateTimeOffset start = orderedSectionEvents.First().OccurredAtUtc;
        DateTimeOffset end = orderedSectionEvents.Last().OccurredAtUtc;

        return new BehaviorSection(
            SectionId: sectionId.ToString(),
            ConceptId: ResolveConceptId(sectionId, questions),
            SectionStartTime: start.ToUnixTimeMilliseconds(),
            SectionEndTime: end.ToUnixTimeMilliseconds(),
            TimeSpentSeconds: CalculateObservedEventSpanSeconds(orderedSectionEvents),
            ScrollSpeedAvgPxPerSec: AverageOrNull(sectionEvents.Select(item => item.ScrollSpeedAvgPxPerSec)),
            ScrollDirectionChanges: SumOrNull(sectionEvents.Select(item => item.ScrollDirectionChanges)),
            // AI1 currently requires content_progression_pct to be numeric. Until both APIs
            // support null for this field, zero is the compatibility fallback when unobserved.
            ContentProgressionPct: LatestOrZero(sectionEvents
                .Where(item => item.ContentProgressionPct.HasValue)
                .Select(item => item.ContentProgressionPct!.Value)),
            SectionRevisitCount: CalculateRevisitCount(sectionId, allEvents),
            InteractionCount: SumOrNull(sectionEvents.Select(item => item.InteractionCount)),
            MicroChallenges: BuildMicroChallenges(sectionEvents, allEvents, answers),
            BackgroundCount: SumOrNull(sectionEvents.Select(item => item.BackgroundCount)),
            TotalBackgroundSeconds: SumDoubleOrNull(sectionEvents.Select(item => item.BackgroundDurationSeconds)),
            TabHiddenCount: HasTabVisibilityTelemetry(allEvents)
                ? CountEvents(sectionEvents, StudySessionBehaviorEventType.TabHidden)
                : null);
    }

    private static string ResolveConceptId(Guid sectionId, IReadOnlyCollection<StudySessionQuestion> questions)
    {
        string? conceptId = questions
            .Where(item => item.StudyMaterialSectionId == sectionId)
            .Select(item => item.AiConceptId)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));

        return conceptId ?? $"section-{sectionId}";
    }

    private static BehaviorMicroChallenge[] BuildMicroChallenges(
        IReadOnlyCollection<StudySessionBehaviorEvent> sectionEvents,
        IReadOnlyCollection<StudySessionBehaviorEvent> allEvents,
        IReadOnlyCollection<StudySessionQuestionAnswer> answers)
    {
        return sectionEvents
            .Where(item => item.EventType == StudySessionBehaviorEventType.QuestionAnswered && item.StudySessionQuestionId.HasValue)
            .OrderBy(item => item.OccurredAtUtc)
            .Select(answered => BuildMicroChallenge(answered, allEvents, answers))
            .Where(item => item is not null)
            .Select(item => item!)
            .ToArray();
    }

    private static BehaviorMicroChallenge? BuildMicroChallenge(
        StudySessionBehaviorEvent answeredEvent,
        IReadOnlyCollection<StudySessionBehaviorEvent> allEvents,
        IReadOnlyCollection<StudySessionQuestionAnswer> answers)
    {
        Guid questionId = answeredEvent.StudySessionQuestionId!.Value;

        StudySessionBehaviorEvent? shownEvent = allEvents
            .Where(item => item.EventType == StudySessionBehaviorEventType.QuestionShown
                && item.StudySessionQuestionId == questionId
                && item.OccurredAtUtc <= answeredEvent.OccurredAtUtc)
            .OrderByDescending(item => item.OccurredAtUtc)
            .FirstOrDefault();

        if (shownEvent is null)
        {
            return null;
        }

        StudySessionQuestionAnswer? answer = answers
            .Where(item => item.StudySessionQuestionId == questionId && item.AnsweredAtUtc <= answeredEvent.OccurredAtUtc)
            .OrderByDescending(item => item.AnsweredAtUtc)
            .ThenByDescending(item => item.AttemptNumber)
            .FirstOrDefault();

        if (answer is null)
        {
            return null;
        }

        return new BehaviorMicroChallenge(
            QuestionId: questionId.ToString(),
            ResponseTimeSeconds: Math.Max(0, (answeredEvent.OccurredAtUtc - shownEvent.OccurredAtUtc).TotalSeconds),
            IsCorrect: answer.IsCorrect);
    }

    private static int CalculateRevisitCount(Guid sectionId, IReadOnlyCollection<StudySessionBehaviorEvent> allEvents)
    {
        Guid? previousSection = null;
        int revisits = 0;

        foreach (StudySessionBehaviorEvent item in allEvents.OrderBy(item => item.OccurredAtUtc))
        {
            if (!item.StudyMaterialSectionId.HasValue)
            {
                continue;
            }

            Guid current = item.StudyMaterialSectionId.Value;
            if (previousSection.HasValue && current == sectionId && previousSection.Value != sectionId)
            {
                revisits++;
            }

            previousSection = current;
        }

        return revisits;
    }

    // This is an event-observed span, not a true foreground/engagement timer. The current
    // behavior-event contract carries no active-time delta, so the backend cannot distinguish
    // an idle gap between events from active reading. Exact active time requires client telemetry.
    private static double CalculateObservedEventSpanSeconds(IReadOnlyCollection<StudySessionBehaviorEvent> events)
    {
        if (events.Count < 2)
        {
            return 0;
        }

        return Math.Max(0, (events.Max(item => item.OccurredAtUtc) - events.Min(item => item.OccurredAtUtc)).TotalSeconds);
    }

    private static int? SumOrNull(IEnumerable<int?> values)
    {
        int[] observed = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return observed.Length == 0 ? null : observed.Sum();
    }

    private static double? SumDoubleOrNull(IEnumerable<double?> values)
    {
        double[] observed = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return observed.Length == 0 ? null : observed.Sum();
    }

    private static double? AverageOrNull(IEnumerable<double?> values)
    {
        double[] observed = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return observed.Length == 0 ? null : observed.Average();
    }

    private static double LatestOrZero(IEnumerable<double> values)
    {
        double[] data = values.ToArray();
        return data.Length == 0 ? 0 : data[^1];
    }

    private static bool HasTabVisibilityTelemetry(IEnumerable<StudySessionBehaviorEvent> events) =>
        events.Any(item => item.EventType is StudySessionBehaviorEventType.TabHidden or StudySessionBehaviorEventType.TabVisible);

    private static int CountEvents(IEnumerable<StudySessionBehaviorEvent> events, StudySessionBehaviorEventType eventType) =>
        events.Count(item => item.EventType == eventType);
}

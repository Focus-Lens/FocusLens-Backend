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
            TimeSpentSeconds: CalculateActiveDuration(orderedSectionEvents),
            ScrollSpeedAvgPxPerSec: AverageOrZero(sectionEvents.Select(item => item.ScrollSpeedAvgPxPerSec)),
            ScrollDirectionChanges: Sum(sectionEvents.Select(item => item.ScrollDirectionChanges)),
            ContentProgressionPct: LatestOrZero(sectionEvents
                .Where(item => item.ContentProgressionPct.HasValue)
                .Select(item => item.ContentProgressionPct!.Value)),
            SectionRevisitCount: CalculateRevisitCount(sectionId, allEvents),
            InteractionCount: Sum(sectionEvents.Select(item => item.InteractionCount)),
            MicroChallenges: BuildMicroChallenges(sectionEvents, allEvents, answers),
            BackgroundCount: Sum(sectionEvents.Select(item => item.BackgroundCount)),
            TotalBackgroundSeconds: SumDouble(sectionEvents.Select(item => item.BackgroundDurationSeconds)),
            TabHiddenCount: CountEvents(sectionEvents, StudySessionBehaviorEventType.TabHidden));
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

    private static double CalculateActiveDuration(IReadOnlyCollection<StudySessionBehaviorEvent> events)
    {
        if (events.Count < 2)
        {
            return 0;
        }

        return Math.Max(0, (events.Max(item => item.OccurredAtUtc) - events.Min(item => item.OccurredAtUtc)).TotalSeconds);
    }

    private static int Sum(IEnumerable<int?> values) => values.Sum(value => value ?? 0);

    private static double SumDouble(IEnumerable<double?> values) => values.Sum(value => value ?? 0);

    private static double AverageOrZero(IEnumerable<double?> values)
    {
        double[] data = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return data.Length == 0 ? 0 : data.Average();
    }

    private static double LatestOrZero(IEnumerable<double> values)
    {
        double[] data = values.ToArray();
        return data.Length == 0 ? 0 : data[^1];
    }

    private static int CountEvents(IEnumerable<StudySessionBehaviorEvent> events, StudySessionBehaviorEventType eventType) =>
        events.Count(item => item.EventType == eventType);
}

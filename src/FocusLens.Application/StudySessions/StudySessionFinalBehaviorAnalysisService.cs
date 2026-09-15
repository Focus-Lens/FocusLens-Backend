using FocusLens.Application.BehavioralIntelligence;
using FocusLens.Contracts.BehavioralIntelligence;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.StudySessions;

namespace FocusLens.Application.StudySessions;

public sealed class StudySessionFinalBehaviorAnalysisService(
    IBaseRepository<StudySession> sessionRepository,
    IBaseRepository<StudySessionBehaviorEvent> behaviorEventRepository,
    IBaseRepository<StudySessionQuestion> questionRepository,
    IBaseRepository<StudySessionQuestionAnswer> answerRepository,
    IBaseRepository<StudySessionBehaviorWindow> windowRepository,
    IBehavioralIntelligenceClient behavioralIntelligenceClient,
    BehaviorWindowBuilder behaviorWindowBuilder,
    BehaviorWindowTimelineCalculator timelineCalculator,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IStudySessionFinalBehaviorAnalysisService
{
    public async Task<Result<Success>> AnalyzeFinalAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        StudySession? session = await sessionRepository.FirstOrDefaultAsync(
            item => item.Id == sessionId,
            item => item.PauseIntervals);

        if (session?.StartedAtUtc is null)
        {
            return Error.NotFound(
                "StudySessions.NotFound",
                "Study session was not found.");
        }

        if (session.Status is not (
            StudySessionStatus.Completed or StudySessionStatus.Cancelled))
        {
            return Error.Conflict(
                "StudySessionBehaviorWindows.FinalStateInvalid",
                "Final behavior analysis is only available after the study session ends.");
        }

        DateTimeOffset timelineEndUtc = session.Status switch
        {
            StudySessionStatus.Completed when session.CompletedAtUtc.HasValue =>
                session.CompletedAtUtc.Value,

            StudySessionStatus.Cancelled when session.CancelledAtUtc.HasValue =>
                session.CancelledAtUtc.Value,

            _ => timeProvider.GetUtcNow()
        };

        BehaviorPauseInterval[] pauses = session.PauseIntervals
            .Where(item => item.EndedAtUtc.HasValue)
            .Select(item => new BehaviorPauseInterval(
                item.StartedAtUtc,
                item.EndedAtUtc!.Value))
            .ToArray();

        IReadOnlyCollection<BehaviorWindowTimelineItem> timeline;

        try
        {
            timeline = timelineCalculator.Calculate(
                session.StartedAtUtc.Value,
                pauses,
                timelineEndUtc,
                true);
        }
        catch (ArgumentException)
        {
            return Error.Failure(
                "StudySessionBehaviorWindows.TimelineInvalid",
                "The persisted pause timeline is invalid.");
        }

        StudySessionQuestion[] questions =
            (await questionRepository.GetAllAsync(
                item => item.StudySessionId == session.Id))
            .OrderBy(item => item.Order)
            .ToArray();

        Guid[] questionIds = questions
            .Select(item => item.Id)
            .ToArray();

        StudySessionQuestionAnswer[] answers = questionIds.Length == 0
            ? []
            : (await answerRepository.GetAllAsync(
                    item => questionIds.Contains(item.StudySessionQuestionId)))
                .OrderBy(item => item.AnsweredAtUtc)
                .ToArray();

        StudySessionBehaviorEvent[] events =
            (await behaviorEventRepository.GetAllAsync(
                item => item.StudySessionId == session.Id &&
                        item.OccurredAtUtc <= timelineEndUtc))
            .OrderBy(item => item.OccurredAtUtc)
            .ToArray();

        StudySessionBehaviorWindow[] persistedWindows =
            (await windowRepository.GetAllAsync(
                item => item.StudySessionId == session.Id))
            .OrderBy(item => item.WindowIndex)
            .ToArray();

        while (true)
        {
            BehaviorWindowTimelineItem? nextWindow =
                timeline.FirstOrDefault(window =>
                    persistedWindows.All(
                        persisted => persisted.WindowIndex != window.WindowIndex));

            if (nextWindow is null)
            {
                return Result.Success;
            }

            BehaviorWindowHistoryItem[] history = persistedWindows
                .Where(item => item.WindowIndex < nextWindow.WindowIndex)
                .Select(ToHistory)
                .ToArray();

            BehaviorWindowRequest aiRequest =
                behaviorWindowBuilder.Build(
                    session.StudentId,
                    session.Id,
                    nextWindow.WindowIndex,
                    nextWindow.WindowStartUtc,
                    nextWindow.WindowEndUtc,
                    nextWindow.IsFinal,
                    events,
                    questions,
                    answers,
                    history);

            BehaviorWindowResponse aiResponse;

            try
            {
                aiResponse = await behavioralIntelligenceClient.AnalyzeWindowAsync(
                    aiRequest,
                    cancellationToken);
            }
            catch (Exception exception)
                when (exception is HttpRequestException or InvalidOperationException)
            {
                return Error.Failure(
                    "StudySessionBehaviorWindows.AnalysisUnavailable",
                    "Behavioral Intelligence analysis is currently unavailable.");
            }

            if (!IsValidResponse(aiResponse, session.Id, nextWindow))
            {
                return Error.Failure(
                    "StudySessionBehaviorWindows.AnalysisInvalid",
                    "Behavioral Intelligence returned an invalid analysis response.");
            }

            Result<StudySessionBehaviorWindow> windowResult =
                StudySessionBehaviorWindow.Create(
                    session.Id,
                    nextWindow.WindowIndex,
                    nextWindow.WindowStartUtc,
                    nextWindow.WindowEndUtc,
                    nextWindow.IsFinal);

            if (windowResult.IsError)
            {
                return windowResult.TopError;
            }

            Result<Success> analysisResult =
                windowResult.Value.RecordAnalysis(
                    aiResponse.WindowFocusScore,
                    aiResponse.WindowState,
                    aiResponse.Trend,
                    aiResponse.WindowUnderstandingScore,
                    aiResponse.UnderstandingTrend,
                    aiResponse.RawAction,
                    aiResponse.RecommendedAction,
                    aiResponse.ActionEmitted);

            if (analysisResult.IsError)
            {
                return analysisResult.TopError;
            }

            windowRepository.Add(windowResult.Value);
            await unitOfWork.SaveChangesAsync();

            persistedWindows =
            [
                ..persistedWindows,
                windowResult.Value
            ];
        }
    }

    private static BehaviorWindowHistoryItem ToHistory(
        StudySessionBehaviorWindow item) =>
        new(
            item.WindowIndex,
            item.FocusScore ?? 0,
            item.FocusState ?? "NORMAL_FOCUSED",
            item.RecommendedAction ?? item.RawAction ?? "CONTINUE",
            item.UnderstandingScore);

    private static bool IsValidResponse(
        BehaviorWindowResponse response,
        Guid sessionId,
        BehaviorWindowTimelineItem window) =>
        Guid.TryParse(response.SessionId, out Guid returnedSessionId) &&
        returnedSessionId == sessionId &&
        response.WindowIndex == window.WindowIndex &&
        response.IsFinal == window.IsFinal &&
        response.WindowFocusScore is not < 0 or > 100 &&
        response.WindowUnderstandingScore is not < 0 or > 100 &&
        !string.IsNullOrWhiteSpace(response.WindowState) &&
        !string.IsNullOrWhiteSpace(response.Trend) &&
        !string.IsNullOrWhiteSpace(response.RawAction) &&
        !string.IsNullOrWhiteSpace(response.RecommendedAction);
}

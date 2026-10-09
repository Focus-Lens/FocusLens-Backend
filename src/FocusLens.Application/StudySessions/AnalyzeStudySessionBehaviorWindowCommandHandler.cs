using FocusLens.Application.BehavioralIntelligence;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Contracts.BehavioralIntelligence;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.StudySessions;
using MediatR;

namespace FocusLens.Application.StudySessions;

public sealed class AnalyzeStudySessionBehaviorWindowCommandHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudySession> sessionRepository,
    IBaseRepository<StudySessionBehaviorEvent> behaviorEventRepository,
    IBaseRepository<StudySessionQuestion> questionRepository,
    IBaseRepository<StudySessionQuestionAnswer> answerRepository,
    IBaseRepository<StudySessionBehaviorWindow> windowRepository,
    IBehavioralIntelligenceClient behavioralIntelligenceClient,
    BehaviorWindowBuilder behaviorWindowBuilder,
    BehaviorWindowTimelineCalculator timelineCalculator,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<AnalyzeStudySessionBehaviorWindowCommand, Result<BehaviorWindowResponse>>
{
    public async Task<Result<BehaviorWindowResponse>> Handle(
        AnalyzeStudySessionBehaviorWindowCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
            return Error.NotFound("StudySessions.NotFound", "Study session was not found.");

        Student? student = await studentRepository.FirstOrDefaultAsync(item => item.UserId == userId);
        if (student is null)
            return Error.NotFound("StudySessions.NotFound", "Study session was not found.");

        StudySession? session = await sessionRepository.FirstOrDefaultAsync(
            item => item.Id == request.SessionId && item.StudentId == student.Id,
            item => item.PauseIntervals);
        if (session?.StartedAtUtc is null)
            return Error.NotFound("StudySessions.NotFound", "Study session was not found.");

        bool sessionEnded = session.Status is StudySessionStatus.Completed or StudySessionStatus.Cancelled;
        if (request.IsFinal != sessionEnded)
        {
            return Error.Conflict("StudySessionBehaviorWindows.FinalStateInvalid", request.IsFinal
                ? "Final behavior analysis is only available after the study session ends."
                : "Only final behavior analysis is available after the study session ends.");
        }

        DateTimeOffset timelineEndUtc = GetTimelineEnd(session, timeProvider.GetUtcNow());
        BehaviorPauseInterval[] pauses = session.PauseIntervals
            .Where(item => item.EndedAtUtc.HasValue)
            .Select(item => new BehaviorPauseInterval(item.StartedAtUtc, item.EndedAtUtc!.Value))
            .ToArray();

        IReadOnlyCollection<BehaviorWindowTimelineItem> timeline;
        try
        {
            timeline = timelineCalculator.Calculate(
                session.StartedAtUtc.Value, pauses, timelineEndUtc, request.IsFinal);
        }
        catch (ArgumentException)
        {
            return Error.Failure("StudySessionBehaviorWindows.TimelineInvalid", "The persisted pause timeline is invalid.");
        }

        StudySessionBehaviorWindow[] persistedWindows = (await windowRepository.GetAllAsync(
                item => item.StudySessionId == session.Id))
            .OrderBy(item => item.WindowIndex)
            .ToArray();
        BehaviorWindowTimelineItem? nextWindow = timeline.FirstOrDefault(item =>
            persistedWindows.All(persisted => persisted.WindowIndex != item.WindowIndex));

        if (nextWindow is null)
        {
            StudySessionBehaviorWindow? latest = persistedWindows.LastOrDefault();
            return latest is not null
                ? ToResponse(latest)
                : Error.Conflict("StudySessionBehaviorWindows.NotReady",
                    "The study session has not accumulated enough active time for behavior analysis.");
        }

        if (nextWindow.IsFinal != request.IsFinal)
        {
            return Error.Conflict("StudySessionBehaviorWindows.NotReady", nextWindow.IsFinal
                ? "The remaining behavior window must be requested as final analysis."
                : "A complete behavior window must be analyzed before final analysis.");
        }

        StudySessionBehaviorEvent[] events = (await behaviorEventRepository.GetAllAsync(
                item => item.StudySessionId == session.Id && item.OccurredAtUtc <= nextWindow.WindowEndUtc))
            .OrderBy(item => item.OccurredAtUtc)
            .ToArray();
        StudySessionQuestion[] questions = (await questionRepository.GetAllAsync(
                item => item.StudySessionId == session.Id))
            .OrderBy(item => item.Order)
            .ToArray();
        Guid[] questionIds = questions.Select(item => item.Id).ToArray();
        StudySessionQuestionAnswer[] answers = questionIds.Length == 0
            ? []
            : (await answerRepository.GetAllAsync(item => questionIds.Contains(item.StudySessionQuestionId)))
                .OrderBy(item => item.AnsweredAtUtc)
                .ToArray();

        BehaviorWindowHistoryItem[] history = persistedWindows
            .Where(item => item.WindowIndex < nextWindow.WindowIndex)
            .Select(ToHistory)
            .ToArray();
        BehaviorWindowRequest aiRequest = behaviorWindowBuilder.Build(
            student.Id, session.Id, nextWindow.WindowIndex, nextWindow.WindowStartUtc,
            nextWindow.WindowEndUtc, nextWindow.IsFinal, events, questions, answers, history);

        BehaviorWindowResponse aiResponse;
        try
        {
            aiResponse = await behavioralIntelligenceClient.AnalyzeWindowAsync(aiRequest, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            return Error.Failure("StudySessionBehaviorWindows.AnalysisUnavailable",
                "Behavioral Intelligence analysis is currently unavailable.");
        }

        if (!IsValidResponse(aiResponse, session.Id, nextWindow))
            return Error.Failure("StudySessionBehaviorWindows.AnalysisInvalid",
                "Behavioral Intelligence returned an invalid analysis response.");

        Result<StudySessionBehaviorWindow> windowResult = StudySessionBehaviorWindow.Create(
            session.Id, nextWindow.WindowIndex, nextWindow.WindowStartUtc,
            nextWindow.WindowEndUtc, nextWindow.IsFinal);
        if (windowResult.IsError)
            return windowResult.TopError;

        Result<Success> analysisResult = windowResult.Value.RecordAnalysis(
            aiResponse.WindowFocusScore, aiResponse.WindowState, aiResponse.Trend,
            aiResponse.WindowUnderstandingScore, aiResponse.UnderstandingTrend,
            aiResponse.RawAction, aiResponse.RecommendedAction, aiResponse.ActionEmitted,
            aiResponse.WindowActiveTimeSeconds);
        if (analysisResult.IsError)
            return analysisResult.TopError;

        windowRepository.Add(windowResult.Value);
        await unitOfWork.SaveChangesAsync();
        return aiResponse;
    }

    private static DateTimeOffset GetTimelineEnd(StudySession session, DateTimeOffset now) => session.Status switch
    {
        StudySessionStatus.Completed when session.CompletedAtUtc.HasValue => session.CompletedAtUtc.Value,
        StudySessionStatus.Cancelled when session.CancelledAtUtc.HasValue => session.CancelledAtUtc.Value,
        StudySessionStatus.Paused when session.PausedAtUtc.HasValue => session.PausedAtUtc.Value,
        _ => now
    };

    private static BehaviorWindowHistoryItem ToHistory(StudySessionBehaviorWindow item) => new(
        item.WindowIndex, item.FocusScore, item.FocusState ?? "NORMAL_FOCUSED",
        item.RawAction ?? "CONTINUE", item.UnderstandingScore);

    private static Result<BehaviorWindowResponse> ToResponse(StudySessionBehaviorWindow item) => new BehaviorWindowResponse(
        item.StudySessionId.ToString(), item.WindowIndex, item.FocusScore, item.UnderstandingScore,
        item.UnderstandingTrend, item.FocusState ?? "NORMAL_FOCUSED", item.RecommendedAction ?? "CONTINUE",
        item.RawAction ?? "CONTINUE", item.ActionEmitted ?? false, item.FocusTrend ?? "STABLE",
        item.IsFinal, 0, [])
        {
            WindowActiveTimeSeconds = item.WindowActiveTimeSeconds
        };

    private static bool IsValidResponse(BehaviorWindowResponse response, Guid sessionId,
        BehaviorWindowTimelineItem window) =>
        Guid.TryParse(response.SessionId, out Guid returnedSessionId) && returnedSessionId == sessionId &&
        response.WindowIndex == window.WindowIndex && response.IsFinal == window.IsFinal &&
        response.WindowFocusScore is not < 0 or > 100 &&
        response.WindowUnderstandingScore is not < 0 or > 100 &&
        response.WindowActiveTimeSeconds is double activeSeconds &&
        double.IsFinite(activeSeconds) && activeSeconds >= 0 &&
        !string.IsNullOrWhiteSpace(response.WindowState) && !string.IsNullOrWhiteSpace(response.Trend) &&
        !string.IsNullOrWhiteSpace(response.RawAction) && !string.IsNullOrWhiteSpace(response.RecommendedAction);
}

using FocusLens.Domain.Common;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Domain.StudySessions;

public sealed class StudySessionBehaviorWindow : AuditableEntity
{
    private StudySessionBehaviorWindow() { }

    private StudySessionBehaviorWindow(
        Guid studySessionId,
        int windowIndex,
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        bool isFinal)
        : base(Guid.CreateVersion7())
    {
        StudySessionId = studySessionId;
        WindowIndex = windowIndex;
        WindowStartUtc = windowStartUtc;
        WindowEndUtc = windowEndUtc;
        IsFinal = isFinal;
    }

    public Guid StudySessionId { get; private set; }
    public int WindowIndex { get; private set; }
    public DateTimeOffset WindowStartUtc { get; private set; }
    public DateTimeOffset WindowEndUtc { get; private set; }
    public bool IsFinal { get; private set; }
    public int? FocusScore { get; private set; }
    public string? FocusState { get; private set; }
    public string? FocusTrend { get; private set; }
    public int? UnderstandingScore { get; private set; }
    public string? UnderstandingTrend { get; private set; }
    public string? RawAction { get; private set; }
    public string? RecommendedAction { get; private set; }
    public bool? ActionEmitted { get; private set; }

    public static Result<StudySessionBehaviorWindow> Create(
        Guid studySessionId,
        int windowIndex,
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        bool isFinal)
    {
        if (studySessionId == Guid.Empty)
        {
            return Error.Validation("StudySessionBehaviorWindows.SessionIdRequired", "Study session id is required.");
        }

        if (windowIndex <= 0 || windowEndUtc < windowStartUtc)
        {
            return Error.Validation("StudySessionBehaviorWindows.RangeInvalid", "Behavior window range is invalid.");
        }

        return new StudySessionBehaviorWindow(studySessionId, windowIndex, windowStartUtc, windowEndUtc, isFinal);
    }

    public Result<Success> RecordAnalysis(
        int? focusScore,
        string? focusState,
        string? focusTrend,
        int? understandingScore,
        string? understandingTrend,
        string? rawAction,
        string? recommendedAction,
        bool actionEmitted)
    {
        if (focusScore is < 0 or > 100 || understandingScore is < 0 or > 100)
        {
            return Error.Validation("StudySessionBehaviorWindows.ScoreInvalid", "Behavior analysis score is invalid.");
        }

        FocusScore = focusScore;
        FocusState = Normalize(focusState);
        FocusTrend = Normalize(focusTrend);
        UnderstandingScore = understandingScore;
        UnderstandingTrend = Normalize(understandingTrend);
        RawAction = Normalize(rawAction);
        RecommendedAction = Normalize(recommendedAction);
        ActionEmitted = actionEmitted;
        return Result.Success;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

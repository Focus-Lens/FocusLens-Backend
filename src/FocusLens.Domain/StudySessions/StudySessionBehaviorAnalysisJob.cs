using FocusLens.Domain.Common.Results;

namespace FocusLens.Domain.StudySessions;

public sealed class StudySessionBehaviorAnalysisJob
{
    private const int MaxErrorLength = 2000;

    private StudySessionBehaviorAnalysisJob()
    {
    }

    private StudySessionBehaviorAnalysisJob(
        Guid studySessionId,
        DateTimeOffset createdAtUtc)
    {
        Id = Guid.NewGuid();
        StudySessionId = studySessionId;
        Status = StudySessionBehaviorAnalysisJobStatus.Pending;
        AttemptCount = 0;
        CreatedAtUtc = createdAtUtc;
        NextAttemptAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid StudySessionId { get; private set; }

    public StudySessionBehaviorAnalysisJobStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ProcessingStartedAtUtc { get; private set; }

    public DateTimeOffset? NextAttemptAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public static Result<StudySessionBehaviorAnalysisJob> Create(
        Guid studySessionId,
        DateTimeOffset createdAtUtc)
    {
        if (studySessionId == Guid.Empty)
        {
            return Error.Validation(
                "StudySessionBehaviorAnalysisJobs.SessionRequired",
                "Study session id is required.");
        }

        return new StudySessionBehaviorAnalysisJob(
            studySessionId,
            createdAtUtc);
    }

    public Result<Success> MarkProcessing(DateTimeOffset startedAtUtc)
    {
        if (Status == StudySessionBehaviorAnalysisJobStatus.Completed)
        {
            return Error.Conflict(
                "StudySessionBehaviorAnalysisJobs.AlreadyCompleted",
                "The behavior analysis job is already completed.");
        }

        Status = StudySessionBehaviorAnalysisJobStatus.Processing;
        AttemptCount++;
        ProcessingStartedAtUtc = startedAtUtc;
        NextAttemptAtUtc = null;
        LastError = null;

        return Result.Success;
    }

    public Result<Success> ScheduleRetry(
        DateTimeOffset nextAttemptAtUtc,
        string error)
    {
        if (Status == StudySessionBehaviorAnalysisJobStatus.Completed)
        {
            return Error.Conflict(
                "StudySessionBehaviorAnalysisJobs.AlreadyCompleted",
                "The behavior analysis job is already completed.");
        }

        Status = StudySessionBehaviorAnalysisJobStatus.Pending;
        ProcessingStartedAtUtc = null;
        NextAttemptAtUtc = nextAttemptAtUtc;
        LastError = error.Length > MaxErrorLength
            ? error[..MaxErrorLength]
            : error;

        return Result.Success;
    }

    public Result<Success> MarkCompleted(DateTimeOffset completedAtUtc)
    {
        Status = StudySessionBehaviorAnalysisJobStatus.Completed;
        ProcessingStartedAtUtc = null;
        NextAttemptAtUtc = null;
        CompletedAtUtc = completedAtUtc;
        LastError = null;

        return Result.Success;
    }
}

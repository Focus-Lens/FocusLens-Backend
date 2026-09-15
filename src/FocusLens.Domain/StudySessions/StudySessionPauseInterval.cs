using FocusLens.Domain.Common;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Domain.StudySessions;

public sealed class StudySessionPauseInterval : Entity
{
    private StudySessionPauseInterval() { }
    internal StudySessionPauseInterval(Guid studySessionId, DateTimeOffset startedAtUtc) : base(Guid.CreateVersion7())
    {
        StudySessionId = studySessionId;
        StartedAtUtc = startedAtUtc;
    }

    public Guid StudySessionId { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? EndedAtUtc { get; private set; }

    internal Result<Success> Close(DateTimeOffset endedAtUtc)
    {
        if (EndedAtUtc is not null || endedAtUtc < StartedAtUtc)
            return Error.Validation("StudySessionPauseIntervals.InvalidClose", "Pause interval cannot be closed.");
        EndedAtUtc = endedAtUtc;
        return Result.Success;
    }
}

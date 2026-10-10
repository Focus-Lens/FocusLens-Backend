using FocusLens.Domain.Common;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Domain.StudySessions;

public sealed class StudySessionSelectedSection : Entity
{
    private StudySessionSelectedSection()
    {
    }

    internal StudySessionSelectedSection(
        Guid studyMaterialSectionId,
        int estimatedDurationMinutes,
        int order)
        : base(Guid.CreateVersion7())
    {
        StudyMaterialSectionId = studyMaterialSectionId;
        EstimatedDurationMinutes = estimatedDurationMinutes;
        Order = order;
    }

    public Guid StudySessionSelectionId { get; private set; }

    public Guid StudyMaterialSectionId { get; private set; }

    public int EstimatedDurationMinutes { get; private set; }

    public int Order { get; private set; }

    public DateTimeOffset? StartedAtUtc { get; private set; }

    public DateTimeOffset? ChallengeAvailableAtUtc { get; private set; }

    public DateTimeOffset? ChallengeDeferredUntilUtc { get; private set; }

    public DateTimeOffset? ChallengePostponedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public bool IsChallengeAvailable =>
        StartedAtUtc is not null &&
        CompletedAtUtc is null &&
        ChallengeAvailableAtUtc is not null;

    public Result<Success> Start(DateTimeOffset startedAtUtc)
    {
        if (StartedAtUtc is not null)
        {
            return Error.Conflict(
                "StudySessionSelectedSections.AlreadyStarted",
                "This study section has already started.");
        }

        StartedAtUtc = startedAtUtc;

        return Result.Success;
    }

    public Result<Success> MakeChallengeAvailable(DateTimeOffset availableAtUtc)
    {
        if (StartedAtUtc is null)
        {
            return Error.Validation("StudySessionSelectedSections.NotStarted", "The study section has not started.");
        }

        if (CompletedAtUtc is not null)
        {
            return Error.Conflict("StudySessionSelectedSections.AlreadyCompleted",
                "This study section has already been completed.");
        }

        if (ChallengeAvailableAtUtc is not null)
        {
            return Result.Success;
        }

        ChallengeAvailableAtUtc = availableAtUtc;
        return Result.Success;
    }

    public Result<Success> Complete(DateTimeOffset completedAtUtc)
    {
        if (StartedAtUtc is null)
        {
            return Error.Validation(
                "StudySessionSelectedSections.NotStarted",
                "The study section has not started.");
        }

        if (CompletedAtUtc is not null)
        {
            return Error.Conflict(
                "StudySessionSelectedSections.AlreadyCompleted",
                "This study section has already been completed.");
        }

        CompletedAtUtc = completedAtUtc;

        return Result.Success;
    }

    public bool IsChallengeAvailableAt(DateTimeOffset utcNow) =>
        IsChallengeAvailable &&
        (ChallengeDeferredUntilUtc is null || ChallengeDeferredUntilUtc <= utcNow);

    public Result<Success> PostponeChallenge(DateTimeOffset utcNow, TimeSpan duration)
    {
        if (ChallengePostponedAtUtc is not null)
        {
            return Error.Conflict(
                "StudySessionSelectedSections.ChallengeAlreadyPostponed",
                "This challenge has already used its one allowed postponement.");
        }

        if (!IsChallengeAvailableAt(utcNow))
        {
            return Error.Conflict(
                "StudySessionSelectedSections.ChallengeUnavailable",
                "Only an available challenge can be postponed.");
        }

        if (duration <= TimeSpan.Zero)
        {
            return Error.Validation(
                "StudySessionSelectedSections.PostponementInvalid",
                "Challenge postponement must be positive.");
        }

        ChallengePostponedAtUtc = utcNow;
        ChallengeDeferredUntilUtc = utcNow.Add(duration);
        return Result.Success;
    }
}
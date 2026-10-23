using FocusLens.Domain.Common;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;

namespace FocusLens.Domain.StudySessions;

public sealed class StudySession : AuditableEntity
{
    private readonly List<StudySessionCompletedSection> _completedSections = [];
    private readonly List<StudySessionPauseInterval> _pauseIntervals = [];

    private StudySession() { }

    private StudySession(Guid studentId, StudySessionMode mode, StudySessionType sessionType)
        : base(Guid.CreateVersion7())
    {
        StudentId = studentId;
        Mode = mode;
        SessionType = sessionType;
        Status = StudySessionStatus.Draft;
    }

    public Guid StudentId { get; }

    public StudySessionMode Mode { get; private set; }

    public StudySessionType SessionType { get; private set; } = StudySessionType.StudySession;

    public StudySessionStatus Status { get; private set; }

    public Guid? SelectedSubjectId { get; private set; }

    public int? FocusDurationMinutes { get; private set; }
    public int ExtendedDurationMinutes { get; private set; }
    public bool IsSlowPaceEnabled { get; private set; }
    public bool IsIncreasedAssessmentFrequencyEnabled { get; private set; }

    public DateTimeOffset? StartedAtUtc { get; private set; }

    public DateTimeOffset? PausedAtUtc { get; private set; }
    public DateTimeOffset? BreakEndsAtUtc { get; private set; }

    public int AccumulatedPausedSeconds { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public Guid? StudyMaterialId { get; private set; }

    public StudyMaterial? Material { get; private set; }

    public StudySessionSelection? Selection { get; private set; }

    public int EstimatedStudyTimeMinutes => Selection?.EstimatedStudyTimeMinutes ?? 0;
    public int? CurrentPage { get; private set; }

    public DateTimeOffset? LastActivityAtUtc { get; private set; }

    public IReadOnlyCollection<StudySessionCompletedSection> CompletedSections =>
        _completedSections.AsReadOnly();

    public IReadOnlyCollection<StudySessionPauseInterval> PauseIntervals => _pauseIntervals.AsReadOnly();

    public static Result<StudySession> Create(
        Guid studentId,
        StudySessionMode mode,
        StudySessionType sessionType = StudySessionType.StudySession)
    {
        if (studentId == Guid.Empty)
        {
            return StudySessionErrors.StudentIdRequired;
        }

        if (!Enum.IsDefined(mode))
        {
            return StudySessionErrors.InvalidMode;
        }

        if (!Enum.IsDefined(sessionType))
        {
            return StudySessionErrors.InvalidSessionType;
        }

        return new StudySession(studentId, mode, sessionType);
    }

    public Result<Success> SetMode(StudySessionMode mode)
    {
        Result<Success> configurable = EnsureConfigurable();
        if (configurable.IsError || !Enum.IsDefined(mode))
        {
            return configurable.IsError ? configurable : StudySessionErrors.InvalidMode;
        }

        Mode = mode;
        MarkDraft();
        return Result.Success;
    }

    public Result<Success> SetSessionType(StudySessionType sessionType)
    {
        Result<Success> configurable = EnsureConfigurable();
        if (configurable.IsError || !Enum.IsDefined(sessionType))
        {
            return configurable.IsError ? configurable : StudySessionErrors.InvalidSessionType;
        }

        SessionType = sessionType;
        MarkDraft();
        return Result.Success;
    }

    public Result<Success> SetSubject(StudentSubject? subject) => SetSubjectId(subject?.Id ?? Guid.Empty);

    public Result<Success> SetSubjectId(Guid subjectId)
    {
        Result<Success> configurable = EnsureConfigurable();
        if (configurable.IsError || subjectId == Guid.Empty)
        {
            return configurable.IsError ? configurable : StudySessionErrors.SubjectRequired;
        }

        SelectedSubjectId = subjectId;
        MarkDraft();
        return Result.Success;
    }

    public Result<Success> SetDuration(int focusDurationMinutes)
    {
        Result<Success> configurable = EnsureConfigurable();
        if (configurable.IsError || focusDurationMinutes <= 0)
        {
            return configurable.IsError ? configurable : StudySessionErrors.DurationInvalid;
        }

        FocusDurationMinutes = focusDurationMinutes;
        MarkDraft();
        return Result.Success;
    }

    public Result<Success> ExtendDuration(int minutes)
    {
        if (Status is not (StudySessionStatus.Active or StudySessionStatus.Paused)) return StudySessionErrors.NotInProgress;
        if (minutes <= 0) return StudySessionErrors.DurationInvalid;
        ExtendedDurationMinutes += minutes;
        return Result.Success;
    }

    public Result<Success> EnableSlowPace()
    {
        if (Status is not (StudySessionStatus.Active or StudySessionStatus.Paused)) return StudySessionErrors.NotInProgress;
        IsSlowPaceEnabled = true;
        return Result.Success;
    }

    public Result<Success> EnableIncreasedAssessmentFrequency()
    {
        if (Status is not (StudySessionStatus.Active or StudySessionStatus.Paused)) return StudySessionErrors.NotInProgress;
        IsIncreasedAssessmentFrequencyEnabled = true;
        return Result.Success;
    }

    public TimeSpan GetRequiredSectionActiveDuration(
        int estimatedDurationMinutes,
        int completedChallengeCount = 0)
    {
        if (estimatedDurationMinutes <= 0 || completedChallengeCount < 0)
        {
            return TimeSpan.Zero;
        }

        // Slow pace intentionally gives the learner 25% more active study
        // time before the next adaptive check-in. This applies in paper mode
        // directly and in digital mode once the learner reaches the section
        // boundary.
        double multiplier = IsSlowPaceEnabled ? 1.25 : 1.0;
        // More frequent check-ins occur halfway through the normal section
        // cadence. When combined with slow pace, both interventions apply.
        if (IsIncreasedAssessmentFrequencyEnabled)
        {
            multiplier *= 0.5;
        }
        return TimeSpan.FromMinutes(
            estimatedDurationMinutes * multiplier * (completedChallengeCount + 1));
    }

    public int GetDigitalChallengeTriggerPage(int fromPage, int toPage, int completedChallengeCount = 0)
    {
        if (fromPage <= 0 || toPage < fromPage)
        {
            throw new ArgumentOutOfRangeException(nameof(fromPage));
        }

        if (!IsIncreasedAssessmentFrequencyEnabled || completedChallengeCount > 0)
        {
            return toPage;
        }

        int pageCount = toPage - fromPage + 1;
        return fromPage + (int)Math.Ceiling(pageCount / 2.0) - 1;
    }

    public Result<Success> SetStudyMaterial(StudyMaterial? material)
    {
        Result<Success> configurable = EnsureConfigurable();
        if (configurable.IsError || material is null || material.StudentId != StudentId)
        {
            return configurable.IsError ? configurable : StudySessionErrors.MaterialMismatch;
        }

        StudyMaterialId = material.Id;
        Material = material;
        Selection = null;
        CurrentPage = null;
        _completedSections.Clear();
        MarkDraft();
        return Result.Success;
    }

    public Result<Success> SetSelection(StudySessionSelection? selection)
    {
        Result<Success> configurable = EnsureConfigurable();
        if (configurable.IsError)
        {
            return configurable;
        }

        if (selection is null || Material is null)
        {
            return StudySessionErrors.PageRangeRequiresMaterial;
        }

        if (selection.StudySessionId != Id || selection.StudyMaterialId != Material.Id)
        {
            return StudySessionErrors.SelectionMaterialMismatch;
        }

        if (selection.ToPage > Material.PageCount)
        {
            return StudySessionErrors.PageRangeExceedsMaterial;
        }

        Selection = selection;
        CurrentPage = selection.FromPage;
        _completedSections.Clear();
        MarkDraft();
        return Result.Success;
    }

    public Result<Success> SetSelectedSections(IEnumerable<StudyMaterialSection>? sections)
    {
        Result<Success> configurable = EnsureConfigurable();
        if (configurable.IsError)
        {
            return configurable;
        }

        if (Material is null || Selection is null)
        {
            return StudySessionErrors.SectionsRequireMaterial;
        }

        List<StudyMaterialSection> selectedSections = sections?.ToList() ?? [];
        if (selectedSections.Any(section => section.StudyMaterialId != Material.Id))
        {
            return StudySessionErrors.SectionMismatch;
        }

        if (
            selectedSections.Select(section => section.Id).Distinct().Count()
            != selectedSections.Count
        )
        {
            return StudySessionErrors.DuplicateSection;
        }

        Result<Success> selectionResult = Selection.SetSelectedSections(selectedSections);
        if (selectionResult.IsError)
        {
            return selectionResult;
        }

        _completedSections.Clear();
        MarkDraft();
        return Result.Success;
    }

    public Result<Success> MarkReady()
    {
        if (Status != StudySessionStatus.Draft)
        {
            return StudySessionErrors.NotConfigurable;
        }

        if (SelectedSubjectId is null ||
            (FocusDurationMinutes is null && EstimatedStudyTimeMinutes <= 0))
        {
            return StudySessionErrors.NotReady;
        }

        if (Material is null)
        {
            return StudySessionErrors.MaterialRequired;
        }

        Status = StudySessionStatus.Ready;
        return Result.Success;
    }

    public Result<Success> Start(DateTimeOffset startedAtUtc)
    {
        if (Status != StudySessionStatus.Ready)
        {
            return StudySessionErrors.NotReady;
        }

        Status = StudySessionStatus.Active;
        StartedAtUtc = startedAtUtc;
        PausedAtUtc = null;
        AccumulatedPausedSeconds = 0;
        CompletedAtUtc = null;
        CancelledAtUtc = null;
        // An active session is only live while the client continues to report
        // activity.  This also gives a newly started session a deterministic
        // heartbeat baseline.
        LastActivityAtUtc = startedAtUtc;
        return Result.Success;
    }

    public Result<Success> Pause(DateTimeOffset pausedAtUtc)
    {
        if (Status != StudySessionStatus.Active)
        {
            return StudySessionErrors.NotActive;
        }

        Status = StudySessionStatus.Paused;
        PausedAtUtc = pausedAtUtc;
        _pauseIntervals.Add(new StudySessionPauseInterval(Id, pausedAtUtc));
        return Result.Success;
    }

    public Result<Success> StartBreak(DateTimeOffset startedAtUtc, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) return StudySessionErrors.DurationInvalid;
        Result<Success> pause = Pause(startedAtUtc);
        if (pause.IsError) return pause;
        BreakEndsAtUtc = startedAtUtc.Add(duration);
        return Result.Success;
    }

    public Result<Success> Resume(DateTimeOffset resumedAtUtc)
    {
        if (Status != StudySessionStatus.Paused || PausedAtUtc is null)
        {
            return StudySessionErrors.NotPaused;
        }

        AccumulatePause(resumedAtUtc);
        CloseOpenPauseInterval(resumedAtUtc);
        Status = StudySessionStatus.Active;
        PausedAtUtc = null;
        BreakEndsAtUtc = null;
        return Result.Success;
    }

    public Result<Success> End(DateTimeOffset endedAtUtc)
    {
        if (Status is not (StudySessionStatus.Active or StudySessionStatus.Paused))
        {
            return StudySessionErrors.NotInProgress;
        }

        if (Status == StudySessionStatus.Paused)
        {
            AccumulatePause(endedAtUtc);
            CloseOpenPauseInterval(endedAtUtc);
            PausedAtUtc = null;
        }

        Status = StudySessionStatus.Cancelled;
        CancelledAtUtc = endedAtUtc;
        return Result.Success;
    }

    public Result<Success> CompleteSuccessfully(DateTimeOffset completedAtUtc)
    {
        if (Status is not (StudySessionStatus.Active or StudySessionStatus.Paused))
        {
            return StudySessionErrors.NotInProgress;
        }

        if (Status == StudySessionStatus.Paused)
        {
            AccumulatePause(completedAtUtc);
            CloseOpenPauseInterval(completedAtUtc);
            PausedAtUtc = null;
        }

        Status = StudySessionStatus.Completed;
        CompletedAtUtc = completedAtUtc;
        return Result.Success;
    }

    public Result<Success> UpdateProgress(
        int? currentPage,
        DateTimeOffset activityAtUtc)
    {
        if (Status != StudySessionStatus.Active)
        {
            return StudySessionErrors.NotActive;
        }

        if (currentPage is not null)
        {
            if (Selection is null
                || currentPage < Selection.FromPage
                || Material is null
                || currentPage > Material.PageCount)
            {
                return StudySessionErrors.CurrentPageOutsideRange;
            }

            CurrentPage = currentPage;
        }

        // Retries and delayed requests must never move the activity watermark
        // backwards (or make a previously counted interval count again).
        if (LastActivityAtUtc is null || activityAtUtc > LastActivityAtUtc.Value)
        {
            LastActivityAtUtc = activityAtUtc;
        }

        return Result.Success;
    }

    public Result<Success> MarkSectionCompleted(Guid studyMaterialSectionId)
    {
        if (studyMaterialSectionId == Guid.Empty)
        {
            return StudySessionErrors.SectionMismatch;
        }

        bool isSelected = Selection?.SelectedSections
            .Any(section => section.StudyMaterialSectionId == studyMaterialSectionId) == true;
        if (!isSelected)
        {
            return StudySessionErrors.SectionMismatch;
        }

        if (_completedSections.All(section => section.StudyMaterialSectionId != studyMaterialSectionId))
        {
            _completedSections.Add(new StudySessionCompletedSection(studyMaterialSectionId));
        }

        return Result.Success;
    }

    public TimeSpan GetRemainingDuration(DateTimeOffset utcNow)
    {
        TimeSpan elapsed = GetElapsedDuration(utcNow);
        int durationMinutes = (EstimatedStudyTimeMinutes > 0
            ? EstimatedStudyTimeMinutes
            : FocusDurationMinutes ?? 0) + ExtendedDurationMinutes;

        if (durationMinutes <= 0)
        {
            return TimeSpan.Zero;
        }

        TimeSpan remaining = TimeSpan.FromMinutes(durationMinutes) - elapsed;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public TimeSpan GetOvertimeDuration(DateTimeOffset utcNow)
    {
        TimeSpan elapsed = GetElapsedDuration(utcNow);
        int durationMinutes = (EstimatedStudyTimeMinutes > 0
            ? EstimatedStudyTimeMinutes
            : FocusDurationMinutes ?? 0) + ExtendedDurationMinutes;
        TimeSpan overtime = elapsed - TimeSpan.FromMinutes(durationMinutes);
        return durationMinutes > 0 && overtime > TimeSpan.Zero ? overtime : TimeSpan.Zero;
    }

    /// <summary>
    /// Returns the amount of active study time after a section began.  It is
    /// deliberately based on the recorded pause intervals rather than the
    /// session-wide accumulated pause total, because a section can begin
    /// after earlier pauses have already occurred.
    /// </summary>
    public TimeSpan GetActiveDurationSince(DateTimeOffset startedAtUtc, DateTimeOffset utcNow)
    {
        if (StartedAtUtc is null || Status == StudySessionStatus.Cancelled)
        {
            return TimeSpan.Zero;
        }

        DateTimeOffset effectiveNow = Status switch
        {
            StudySessionStatus.Paused when PausedAtUtc is not null => PausedAtUtc.Value,
            StudySessionStatus.Completed when CompletedAtUtc is not null => CompletedAtUtc.Value,
            _ => utcNow
        };

        if (effectiveNow <= startedAtUtc)
        {
            return TimeSpan.Zero;
        }

        TimeSpan paused = TimeSpan.Zero;
        foreach (StudySessionPauseInterval interval in _pauseIntervals)
        {
            DateTimeOffset intervalStart = interval.StartedAtUtc;
            DateTimeOffset intervalEnd = interval.EndedAtUtc ?? effectiveNow;
            DateTimeOffset overlapStart = intervalStart > startedAtUtc ? intervalStart : startedAtUtc;
            DateTimeOffset overlapEnd = intervalEnd < effectiveNow ? intervalEnd : effectiveNow;

            if (overlapEnd > overlapStart)
            {
                paused += overlapEnd - overlapStart;
            }
        }

        TimeSpan active = effectiveNow - startedAtUtc - paused;
        return active > TimeSpan.Zero ? active : TimeSpan.Zero;
    }

    private TimeSpan GetElapsedDuration(DateTimeOffset utcNow)
    {
        if (StartedAtUtc is null || Status == StudySessionStatus.Cancelled)
        {
            return TimeSpan.Zero;
        }

        DateTimeOffset effectiveNow = Status switch
        {
            StudySessionStatus.Paused when PausedAtUtc is not null => PausedAtUtc.Value,
            StudySessionStatus.Completed when CompletedAtUtc is not null => CompletedAtUtc.Value,
            _ => utcNow
        };

        TimeSpan elapsed = effectiveNow - StartedAtUtc.Value -
                           TimeSpan.FromSeconds(AccumulatedPausedSeconds);
        return elapsed > TimeSpan.Zero ? elapsed : TimeSpan.Zero;
    }

    private Result<Success> EnsureConfigurable()
    {
        return Status is StudySessionStatus.Draft or StudySessionStatus.Ready
            ? Result.Success
            : StudySessionErrors.NotConfigurable;
    }

    private void MarkDraft()
    {
        if (Status == StudySessionStatus.Ready)
        {
            Status = StudySessionStatus.Draft;
        }
    }

    private void CloseOpenPauseInterval(DateTimeOffset utcNow)
    {
        StudySessionPauseInterval? interval = _pauseIntervals.LastOrDefault(item => item.EndedAtUtc is null);
        if (interval is null || interval.Close(utcNow).IsError)
        {
            throw new InvalidOperationException("A paused study session must have exactly one open pause interval.");
        }
    }

    private void AccumulatePause(DateTimeOffset utcNow)
    {
        if (PausedAtUtc is null || utcNow <= PausedAtUtc.Value)
        {
            return;
        }

        AccumulatedPausedSeconds += (int)(utcNow - PausedAtUtc.Value).TotalSeconds;
    }
}

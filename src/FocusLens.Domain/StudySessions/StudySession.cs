using FocusLens.Domain.Common;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;

namespace FocusLens.Domain.StudySessions;

public sealed class StudySession : AuditableEntity
{
    private StudySession()
    {
    }

    private StudySession(Guid studentId, StudySessionMode mode)
        : base(Guid.CreateVersion7())
    {
        StudentId = studentId;
        Mode = mode;
        Status = StudySessionStatus.Draft;
    }

    public Guid StudentId { get; private set; }

    public StudySessionMode Mode { get; private set; }

    public StudySessionStatus Status { get; private set; }

    public DateTimeOffset? StartedAtUtc { get; private set; }

    public Guid? SelectedSubjectId { get; private set; }

    public int? FocusDurationMinutes { get; private set; }

    public Guid? StudyMaterialId { get; private set; }

    public StudyMaterial? Material { get; private set; }

    public StudySessionSelection? Selection { get; private set; }

    public int EstimatedStudyTimeMinutes => Selection?.EstimatedStudyTimeMinutes ?? 0;

    public static Result<StudySession> Create(Guid studentId, StudySessionMode mode)
    {
        if (studentId == Guid.Empty)
        {
            return StudySessionErrors.StudentIdRequired;
        }

        if (!Enum.IsDefined(mode))
        {
            return StudySessionErrors.InvalidMode;
        }

        return new StudySession(studentId, mode);
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

    public Result<Success> SetSubject(StudentSubject? subject)
        => SetSubjectId(subject?.Id ?? Guid.Empty);

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

        if (selectedSections.Select(section => section.Id).Distinct().Count() != selectedSections.Count)
        {
            return StudySessionErrors.DuplicateSection;
        }

        Result<Success> selectionResult = Selection.SetSelectedSections(selectedSections);
        if (selectionResult.IsError)
        {
            return selectionResult;
        }

        MarkDraft();
        return Result.Success;
    }

    public Result<Success> MarkReady()
    {
        if (Status != StudySessionStatus.Draft)
        {
            return StudySessionErrors.NotConfigurable;
        }

        if (SelectedSubjectId is null || FocusDurationMinutes is null)
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

        StartedAtUtc = startedAtUtc;
        Status = StudySessionStatus.Active;
        return Result.Success;
    }

    private Result<Success> EnsureConfigurable()
        => Status is StudySessionStatus.Draft or StudySessionStatus.Ready
            ? Result.Success
            : StudySessionErrors.NotConfigurable;

    private void MarkDraft()
    {
        if (Status == StudySessionStatus.Ready)
        {
            Status = StudySessionStatus.Draft;
        }
    }
}

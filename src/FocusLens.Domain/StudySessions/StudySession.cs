using FocusLens.Domain.Common;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;

namespace FocusLens.Domain.StudySessions;

public sealed class StudySession : AuditableEntity
{
    private readonly List<StudySessionSelectedSection> _selectedSections = [];

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

    public Guid? SelectedSubjectId { get; private set; }

    public int? FocusDurationMinutes { get; private set; }

    public Guid? StudyMaterialId { get; private set; }

    public StudyMaterial? Material { get; private set; }

    public StudySessionPageRange? PageRange { get; private set; }

    public IReadOnlyCollection<StudySessionSelectedSection> SelectedSections => _selectedSections.AsReadOnly();

    public int EstimatedStudyTimeMinutes => _selectedSections.Sum(section => section.EstimatedDurationMinutes);

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
        PageRange = null;
        _selectedSections.Clear();
        MarkDraft();
        return Result.Success;
    }

    public Result<Success> SetPageRange(StudySessionPageRange? pageRange)
    {
        Result<Success> configurable = EnsureConfigurable();
        if (configurable.IsError)
        {
            return configurable;
        }

        if (pageRange is null || Material is null)
        {
            return StudySessionErrors.PageRangeRequiresMaterial;
        }

        if (pageRange.ToPage > Material.PageCount)
        {
            return StudySessionErrors.PageRangeExceedsMaterial;
        }

        PageRange = pageRange;
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

        if (Material is null)
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

        _selectedSections.Clear();
        _selectedSections.AddRange(selectedSections.Select(section =>
            new StudySessionSelectedSection(section.Id, section.EstimatedDurationMinutes)));
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

    public Result<Success> Start()
    {
        if (Status != StudySessionStatus.Ready)
        {
            return StudySessionErrors.NotReady;
        }

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

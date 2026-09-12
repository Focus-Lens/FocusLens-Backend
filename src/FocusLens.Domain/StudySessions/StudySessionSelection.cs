using FocusLens.Domain.Common;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Domain.StudySessions;

public sealed class StudySessionSelection : AuditableEntity
{
    private readonly List<StudySessionSelectedSection> _selectedSections = [];

    private StudySessionSelection()
    {
    }

    private StudySessionSelection(Guid studySessionId, Guid studyMaterialId, int fromPage, int toPage)
        : base(Guid.CreateVersion7())
    {
        StudySessionId = studySessionId;
        StudyMaterialId = studyMaterialId;
        FromPage = fromPage;
        ToPage = toPage;
    }

    public Guid StudySessionId { get; private set; }

    public Guid StudyMaterialId { get; }

    public int FromPage { get; private set; }

    public int ToPage { get; private set; }

    public string? DerivedStorageReference { get; private set; }

    public StudySession? Session { get; private set; }

    public IReadOnlyCollection<StudySessionSelectedSection> SelectedSections => _selectedSections.AsReadOnly();

    public int EstimatedStudyTimeMinutes => _selectedSections.Sum(section => section.EstimatedDurationMinutes);

    public static Result<StudySessionSelection> Create(
        StudySession? session,
        StudyMaterial? material,
        int fromPage,
        int toPage)
    {
        if (session?.Material is null || material is null)
        {
            return StudySessionErrors.PageRangeRequiresMaterial;
        }

        if (session.Material.Id != material.Id || session.StudyMaterialId != material.Id)
        {
            return StudySessionErrors.SelectionMaterialMismatch;
        }

        Result<StudySessionPageRange> rangeResult = StudySessionPageRange.Create(fromPage, toPage);
        if (rangeResult.IsError)
        {
            return rangeResult.TopError;
        }

        if (toPage > material.PageCount)
        {
            return StudySessionErrors.PageRangeExceedsMaterial;
        }

        return new StudySessionSelection(session.Id, material.Id, fromPage, toPage);
    }

    public Result<Success> SetDerivedStorageReference(string? storageReference)
    {
        if (string.IsNullOrWhiteSpace(storageReference))
        {
            return Error.Validation("StudySessionSelections.DerivedStorageReferenceRequired",
                "Derived storage reference is required.");
        }

        DerivedStorageReference = storageReference.Trim();
        return Result.Success;
    }

    public Result<Success> SetSelectedSections(IEnumerable<StudyMaterialSection>? sections)
    {
        List<StudyMaterialSection> selectedSections = sections?.ToList() ?? [];
        if (selectedSections.Any(section => section.StudyMaterialId != StudyMaterialId))
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
        return Result.Success;
    }
}
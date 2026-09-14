using FocusLens.Domain.Common;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Domain.StudySessions;

public sealed class StudyMaterialSection : Entity
{
    private StudyMaterialSection()
    {
    }

    private StudyMaterialSection(
        Guid id,
        Guid studyMaterialId,
        string name,
        int estimatedDurationMinutes,
        int fromPage,
        int toPage)
        : base(id)
    {
        StudyMaterialId = studyMaterialId;
        Name = name;
        EstimatedDurationMinutes = estimatedDurationMinutes;
        FromPage = fromPage;
        ToPage = toPage;
    }

    public Guid StudyMaterialId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int EstimatedDurationMinutes { get; private set; }

    public int FromPage { get; private set; }

    public int ToPage { get; private set; }

    public static Result<StudyMaterialSection> Create(
        Guid studyMaterialId,
        string? name,
        int estimatedDurationMinutes,
        int fromPage,
        int toPage)
    {
        if (studyMaterialId == Guid.Empty)
        {
            return Error.Validation("StudyMaterialSections.MaterialIdRequired", "Study material id is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("StudyMaterialSections.NameRequired", "Section name is required.");
        }

        if (estimatedDurationMinutes <= 0)
        {
            return Error.Validation("StudyMaterialSections.DurationInvalid",
                "Estimated duration must be greater than zero.");
        }

        if (fromPage < 1 || toPage < fromPage)
        {
            return Error.Validation(
                "StudyMaterialSections.PageRangeInvalid",
                "The section page range is invalid.");
        }

        return new StudyMaterialSection(
            Guid.CreateVersion7(),
            studyMaterialId,
            name.Trim(),
            estimatedDurationMinutes,
            fromPage,
            toPage);
    }
}

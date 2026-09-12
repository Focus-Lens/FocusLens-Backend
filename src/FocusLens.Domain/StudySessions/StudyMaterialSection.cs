using FocusLens.Domain.Common;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Domain.StudySessions;

public sealed class StudyMaterialSection : Entity
{
    private StudyMaterialSection()
    {
    }

    private StudyMaterialSection(Guid id, Guid studyMaterialId, string name, int estimatedDurationMinutes)
        : base(id)
    {
        StudyMaterialId = studyMaterialId;
        Name = name;
        EstimatedDurationMinutes = estimatedDurationMinutes;
    }

    public Guid StudyMaterialId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int EstimatedDurationMinutes { get; private set; }

    public static Result<StudyMaterialSection> Create(
        Guid studyMaterialId,
        string? name,
        int estimatedDurationMinutes)
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

        return new StudyMaterialSection(
            Guid.CreateVersion7(),
            studyMaterialId,
            name.Trim(),
            estimatedDurationMinutes);
    }
}
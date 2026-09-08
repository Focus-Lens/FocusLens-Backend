using FocusLens.Domain.Common;

namespace FocusLens.Domain.StudySessions;

public sealed class StudySessionSelectedSection : Entity
{
    private StudySessionSelectedSection()
    {
    }

    internal StudySessionSelectedSection(Guid studyMaterialSectionId, int estimatedDurationMinutes)
        : base(Guid.CreateVersion7())
    {
        StudyMaterialSectionId = studyMaterialSectionId;
        EstimatedDurationMinutes = estimatedDurationMinutes;
    }

    public Guid StudySessionSelectionId { get; private set; }

    public Guid StudyMaterialSectionId { get; private set; }

    public int EstimatedDurationMinutes { get; private set; }
}

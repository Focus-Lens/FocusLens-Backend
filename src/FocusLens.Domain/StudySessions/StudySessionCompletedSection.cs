using FocusLens.Domain.Common;

namespace FocusLens.Domain.StudySessions;

public sealed class StudySessionCompletedSection : Entity
{
    private StudySessionCompletedSection() { }

    internal StudySessionCompletedSection(Guid studyMaterialSectionId)
        : base(Guid.CreateVersion7())
    {
        StudyMaterialSectionId = studyMaterialSectionId;
    }

    public Guid StudySessionId { get; private set; }

    public Guid StudyMaterialSectionId { get; private set; }
}

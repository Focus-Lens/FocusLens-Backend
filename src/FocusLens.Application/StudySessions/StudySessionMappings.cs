using FocusLens.Contracts.StudySessions;
using FocusLens.Domain.StudySessions;
using ContractStudyMaterialSource = FocusLens.Contracts.StudySessions.StudyMaterialSource;
using ContractStudySessionMode = FocusLens.Contracts.StudySessions.StudySessionMode;

namespace FocusLens.Application.StudySessions;

internal static class StudySessionMappings
{
    public static StudySessionResponse ToResponse(this StudySession session)
        => new(
            session.Id,
            session.StudentId,
            (ContractStudySessionMode)session.Mode,
            session.Status.ToString(),
            session.StartedAtUtc,
            session.SelectedSubjectId,
            session.FocusDurationMinutes,
            session.EstimatedStudyTimeMinutes);

    public static StudyMaterialResponse ToResponse(this StudyMaterial material)
        => new(
            material.Id,
            material.FileName,
            material.FileSizeBytes,
            material.PageCount,
            material.StorageReference,
            (ContractStudyMaterialSource)material.Source);

    public static StudySessionSelectionResponse ToResponse(this StudySessionSelection selection)
        => new(
            selection.Id,
            selection.StudySessionId,
            selection.StudyMaterialId,
            selection.FromPage,
            selection.ToPage,
            selection.SelectedSections.Select(section => section.StudyMaterialSectionId).ToArray(),
            selection.EstimatedStudyTimeMinutes);
}

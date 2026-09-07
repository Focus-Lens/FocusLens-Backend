using FocusLens.Contracts.StudySessions;
using FocusLens.Domain.StudySessions;
using ContractStudyMaterialSource = FocusLens.Contracts.StudySessions.StudyMaterialSource;
using ContractStudySessionMode = FocusLens.Contracts.StudySessions.StudySessionMode;

namespace FocusLens.Application.StudySessions;

internal static class StudySessionMappings
{
    public static StudySessionResponse ToResponse(
        this StudySession session,
        IReadOnlyCollection<StudyMaterialSection> sections)
        => new(
            session.Id,
            session.StudentId,
            (ContractStudySessionMode)session.Mode,
            session.Status.ToString(),
            session.SelectedSubjectId,
            session.FocusDurationMinutes,
            session.EstimatedStudyTimeMinutes,
            session.Material is null
                ? null
                : new StudyMaterialResponse(
                    session.Material.Id,
                    session.Material.FileName,
                    session.Material.FileSizeBytes,
                    session.Material.PageCount,
                    session.Material.StorageReference,
                    session.Material.DerivedStorageReference,
                    (ContractStudyMaterialSource)session.Material.Source,
                    sections.Select(section => new StudyMaterialSectionResponse(
                        section.Id,
                        section.Name,
                        section.EstimatedDurationMinutes)).ToArray()),
            session.PageRange is null
                ? null
                : new StudySessionPageRangeResponse(
                    session.PageRange.FromPage,
                    session.PageRange.ToPage),
            session.SelectedSections.Select(section => section.StudyMaterialSectionId).ToArray());
}

using FocusLens.Contracts.StudySessions;
using FocusLens.Domain.StudySessions;
using ContractStudyMaterialSource = FocusLens.Contracts.StudySessions.StudyMaterialSource;
using ContractStudySessionMode = FocusLens.Contracts.StudySessions.StudySessionMode;

namespace FocusLens.Application.StudySessions;

internal static class StudySessionMappings
{
    public static StudySessionResponse ToResponse(this StudySession session, DateTimeOffset utcNow)
    {
        return new StudySessionResponse(
            session.Id,
            session.StudentId,
            (ContractStudySessionMode)session.Mode,
            session.Status.ToString(),
            session.StartedAtUtc,
            session.PausedAtUtc,
            session.CompletedAtUtc,
            session.CancelledAtUtc,
            (int)Math.Ceiling(session.GetRemainingDuration(utcNow).TotalSeconds),
            session.CurrentPage,
            session.LastActivityAtUtc,
            session.CompletedSections.Select(section => section.StudyMaterialSectionId).ToArray(),
            session.SelectedSubjectId,
            session.FocusDurationMinutes,
            session.EstimatedStudyTimeMinutes);
    }

    public static StudyMaterialResponse ToResponse(this StudyMaterial material)
    {
        return new StudyMaterialResponse(
            material.Id,
            material.FileName,
            material.FileSizeBytes,
            material.PageCount,
            material.StorageReference,
            (ContractStudyMaterialSource)material.Source);
    }

    public static StudySessionSelectionResponse ToResponse(this StudySessionSelection selection)
    {
        return new StudySessionSelectionResponse(
            selection.Id,
            selection.StudySessionId,
            selection.StudyMaterialId,
            selection.FromPage,
            selection.ToPage,
            selection.SelectedSections.Select(section => section.StudyMaterialSectionId).ToArray(),
            selection.EstimatedStudyTimeMinutes);
    }

    public static StudySessionImageResponse ToResponse(this StudySessionImage image)
    {
        return new StudySessionImageResponse(
            image.Id,
            image.StudySessionId,
            image.OriginalFileName,
            image.ContentType,
            image.FileSizeBytes,
            image.StorageReference,
            image.CreatedAtUtc);
    }
}

namespace FocusLens.Contracts.StudySessions;

public sealed record CreateStudySessionRequest(
    StudySessionMode Mode,
    Guid SubjectId,
    int FocusDurationMinutes);

public sealed record SetStudySessionModeRequest(StudySessionMode Mode);

public sealed record SetStudySessionSubjectRequest(Guid SubjectId);

public sealed record SetStudySessionDurationRequest(int FocusDurationMinutes);

public sealed record SetStudySessionSelectionRequest(int FromPage, int ToPage);

public sealed record StudyMaterialSectionRequest(string Name, int EstimatedDurationMinutes);

public sealed record ReceiveStudySessionSectionsRequest(
    IReadOnlyCollection<StudyMaterialSectionRequest> Sections);

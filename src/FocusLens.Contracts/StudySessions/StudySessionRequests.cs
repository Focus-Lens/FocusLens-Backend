namespace FocusLens.Contracts.StudySessions;

public sealed record CreateStudySessionRequest(StudySessionMode Mode);

public sealed record SetStudySessionModeRequest(StudySessionMode Mode);

public sealed record SetStudySessionSubjectRequest(Guid SubjectId);

public sealed record SetStudySessionDurationRequest(int FocusDurationMinutes);

public sealed record SetStudySessionPageRangeRequest(int FromPage, int ToPage);

public sealed record SetStudySessionSectionsRequest(IReadOnlyCollection<Guid> SectionIds);

public sealed record ChangeStudySessionSettingsRequest(
    StudySessionMode? Mode,
    Guid? SubjectId,
    int? FocusDurationMinutes);

public sealed record StudyMaterialSectionRequest(string Name, int EstimatedDurationMinutes);

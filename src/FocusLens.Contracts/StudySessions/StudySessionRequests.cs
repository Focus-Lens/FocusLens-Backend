namespace FocusLens.Contracts.StudySessions;

public sealed record CreateStudySessionRequest(
    StudySessionMode Mode,
    Guid SubjectId,
    int FocusDurationMinutes,
    StudySessionType SessionType = StudySessionType.StudySession);

public sealed record SetStudySessionModeRequest(StudySessionMode Mode);

public sealed record SetStudySessionTypeRequest(StudySessionType SessionType);

public sealed record SetStudySessionSubjectRequest(Guid SubjectId);

public sealed record SetStudySessionDurationRequest(int FocusDurationMinutes);

public sealed record SetStudySessionSelectionRequest(int FromPage, int ToPage);

public sealed record UpdateStudySessionProgressRequest(int? CurrentPage);
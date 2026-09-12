using FocusLens.Contracts.StudySessions;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.StudySessions;

public sealed record CreateStudySessionCommand(CreateStudySessionRequest Request)
    : IRequest<Result<StudySessionResponse>>;

public sealed record SetStudySessionModeCommand(Guid SessionId, SetStudySessionModeRequest Request)
    : IRequest<Result<Success>>;

public sealed record SetStudySessionSubjectCommand(Guid SessionId, SetStudySessionSubjectRequest Request)
    : IRequest<Result<Success>>;

public sealed record SetStudySessionDurationCommand(Guid SessionId, SetStudySessionDurationRequest Request)
    : IRequest<Result<Success>>;

public sealed record SetStudySessionSelectionCommand(Guid SessionId, SetStudySessionSelectionRequest Request)
    : IRequest<Result<StudySessionSelectionResponse>>;

public sealed record UpdateStudySessionSelectionCommand(Guid SessionId, SetStudySessionSelectionRequest Request)
    : IRequest<Result<StudySessionSelectionResponse>>;

public sealed record ReceiveStudySessionSectionsCommand(Guid SessionId, ReceiveStudySessionSectionsRequest Request)
    : IRequest<Result<StudySessionResponse>>;

public sealed record ChangeStudySessionMaterialCommand(
    Guid SessionId,
    string FileName,
    long FileSizeBytes,
    byte[] Content,
    StudyMaterialSource Source)
    : IRequest<Result<StudyMaterialResponse>>;

public sealed record StartStudySessionCommand(Guid SessionId)
    : IRequest<Result<Success>>;

public sealed record PauseStudySessionCommand(Guid SessionId)
    : IRequest<Result<StudySessionResponse>>;

public sealed record ResumeStudySessionCommand(Guid SessionId)
    : IRequest<Result<StudySessionResponse>>;

public sealed record EndStudySessionCommand(Guid SessionId)
    : IRequest<Result<StudySessionResponse>>;

public sealed record UpdateStudySessionProgressCommand(
    Guid SessionId,
    UpdateStudySessionProgressRequest Request)
    : IRequest<Result<StudySessionResponse>>;

public sealed record UploadStudyMaterialCommand(
    Guid SessionId,
    string FileName,
    long FileSizeBytes,
    byte[] Content,
    StudyMaterialSource Source)
    : IRequest<Result<StudyMaterialResponse>>;
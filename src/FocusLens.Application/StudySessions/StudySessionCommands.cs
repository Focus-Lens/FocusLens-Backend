using FocusLens.Contracts.StudySessions;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.StudySessions;

public sealed record CreateStudySessionCommand(CreateStudySessionRequest Request)
    : IRequest<Result<StudySessionResponse>>;

public sealed record SetStudySessionModeCommand(Guid SessionId, SetStudySessionModeRequest Request)
    : IRequest<Result<StudySessionResponse>>;

public sealed record SetStudySessionSubjectCommand(Guid SessionId, SetStudySessionSubjectRequest Request)
    : IRequest<Result<StudySessionResponse>>;

public sealed record SetStudySessionDurationCommand(Guid SessionId, SetStudySessionDurationRequest Request)
    : IRequest<Result<StudySessionResponse>>;

public sealed record SetStudySessionPageRangeCommand(Guid SessionId, SetStudySessionPageRangeRequest Request)
    : IRequest<Result<StudySessionResponse>>;

public sealed record SetStudySessionSectionsCommand(Guid SessionId, SetStudySessionSectionsRequest Request)
    : IRequest<Result<StudySessionResponse>>;

public sealed record ChangeStudySessionSettingsCommand(Guid SessionId, ChangeStudySessionSettingsRequest Request)
    : IRequest<Result<StudySessionResponse>>;

public sealed record MarkStudySessionReadyCommand(Guid SessionId)
    : IRequest<Result<StudySessionResponse>>;

public sealed record StartStudySessionCommand(Guid SessionId)
    : IRequest<Result<StudySessionResponse>>;

public sealed record UploadStudyMaterialCommand(
    Guid SessionId,
    string FileName,
    long FileSizeBytes,
    byte[] Content,
    StudyMaterialSource Source,
    IReadOnlyCollection<StudyMaterialSectionRequest> Sections)
    : IRequest<Result<StudySessionResponse>>;

using FocusLens.Contracts.StudySessions;
using MediatR;

namespace FocusLens.Application.StudySessions;

public sealed record GetStudySessionQuery(Guid SessionId) : IRequest<StudySessionResponse?>;
public sealed record GetStudySessionDerivedMaterialQuery(Guid SessionId) : IRequest<StudySessionPrivateFileResponse?>;
public sealed record GetStudySessionImageQuery(Guid SessionId, Guid ImageId) : IRequest<StudySessionPrivateFileResponse?>;

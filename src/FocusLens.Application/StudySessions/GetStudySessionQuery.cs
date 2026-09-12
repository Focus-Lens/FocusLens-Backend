using FocusLens.Contracts.StudySessions;
using MediatR;

namespace FocusLens.Application.StudySessions;

public sealed record GetStudySessionQuery(Guid SessionId) : IRequest<StudySessionResponse?>;
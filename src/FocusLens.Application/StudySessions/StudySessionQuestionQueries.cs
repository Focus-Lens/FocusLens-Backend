using FocusLens.Contracts.StudySessions;
using MediatR;

namespace FocusLens.Application.StudySessions;

public sealed record GetCurrentStudySessionQuestionQuery(Guid SessionId)
    : IRequest<StudySessionCurrentQuestionResponse?>;

public sealed record GetStudySessionResultQuery(Guid SessionId)
    : IRequest<StudySessionResultResponse?>;
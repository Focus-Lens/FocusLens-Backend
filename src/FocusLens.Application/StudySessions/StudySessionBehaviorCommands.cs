using FocusLens.Contracts.StudySessions;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.StudySessions;

public sealed record RecordStudySessionBehaviorEventsCommand(
    Guid SessionId,
    RecordStudySessionBehaviorEventsRequest Request)
    : IRequest<Result<StudySessionBehaviorEventsResponse>>;
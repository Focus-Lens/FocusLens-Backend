using FocusLens.Contracts.BehavioralIntelligence;
using MediatR;

namespace FocusLens.Application.StudySessions;

public sealed record GetStudySessionBehaviorWindowsQuery(Guid SessionId)
    : IRequest<IReadOnlyCollection<BehaviorWindowResultResponse>?>;

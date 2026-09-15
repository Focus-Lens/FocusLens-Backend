using FocusLens.Contracts.BehavioralIntelligence;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.StudySessions;

public sealed record AnalyzeStudySessionBehaviorWindowCommand(
    Guid SessionId,
    bool IsFinal)
    : IRequest<Result<BehaviorWindowResponse>>;

using FocusLens.Contracts.Progress;
using MediatR;

namespace FocusLens.Application.Progress;

public sealed record GetBehavioralProgressQuery(
    Guid? StudentId,
    string? Range,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null) : IRequest<BehavioralProgressResponse?>;
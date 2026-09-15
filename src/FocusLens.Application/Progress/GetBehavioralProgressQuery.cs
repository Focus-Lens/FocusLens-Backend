using FocusLens.Contracts.Progress;
using MediatR;

namespace FocusLens.Application.Progress;

public sealed record GetBehavioralProgressQuery(Guid? StudentId, string? Range)
    : IRequest<BehavioralProgressResponse?>;

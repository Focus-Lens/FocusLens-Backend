using FocusLens.Contracts.Progress;
using MediatR;

namespace FocusLens.Application.Progress;

public sealed record GetProgressQuery(
    Guid? StudentId,
    string? Range,
    DateOnly? DateFrom,
    DateOnly? DateTo) : IRequest<ProgressResponse?>;

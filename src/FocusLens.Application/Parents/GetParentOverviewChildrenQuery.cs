using FocusLens.Contracts;
using MediatR;

namespace FocusLens.Application.Parents;

public sealed record GetParentOverviewChildrenQuery
    : IRequest<IReadOnlyList<ParentOverviewChildResponse>>;

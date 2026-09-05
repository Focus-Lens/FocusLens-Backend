using FocusLens.Contracts;

using MediatR;

namespace FocusLens.Application.Access;

public sealed record GetMyParentsQuery : IRequest<IReadOnlyList<ParentResponse>>;

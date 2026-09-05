using FocusLens.Contracts;

using MediatR;

namespace FocusLens.Application.Parents;

public sealed record GetMyParentQuery : IRequest<ParentResponse?>;

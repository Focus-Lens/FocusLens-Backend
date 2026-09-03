using FocusLens.Contracts.Parents;

using MediatR;

namespace FocusLens.Application.Parents;

public sealed record GetMyParentQuery : IRequest<ParentResponse?>;

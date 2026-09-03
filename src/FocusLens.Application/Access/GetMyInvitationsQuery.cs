using FocusLens.Contracts.Access;
using MediatR;

namespace FocusLens.Application.Access;

public sealed record GetMyInvitationsQuery : IRequest<IReadOnlyList<InvitationResponse>>;

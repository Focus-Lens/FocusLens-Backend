using FocusLens.Contracts.Access;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Access;

public sealed record CreateInvitationCommand(
    CreateInvitationRequest Request
) : IRequest<Result<InvitationResponse>>;

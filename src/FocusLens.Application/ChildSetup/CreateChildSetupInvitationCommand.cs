using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.ChildSetup;

public sealed record CreateChildSetupInvitationCommand(
    Guid DraftId,
    CreateChildSetupInvitationRequest Request
) : IRequest<Result<ChildSetupInvitationResponse>>;
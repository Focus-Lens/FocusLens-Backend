using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.ChildSetup;

public sealed record ResendChildSetupInvitationCommand(Guid DraftId)
    : IRequest<Result<ChildSetupInvitationResponse>>;
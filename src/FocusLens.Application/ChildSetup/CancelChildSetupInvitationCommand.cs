using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.ChildSetup;

public sealed record CancelChildSetupInvitationCommand(Guid DraftId)
    : IRequest<Result<Success>>;
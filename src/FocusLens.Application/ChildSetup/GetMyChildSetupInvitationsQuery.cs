using FocusLens.Contracts.ChildSetup;
using MediatR;

namespace FocusLens.Application.ChildSetup;

public sealed record GetMyChildSetupInvitationsQuery
    : IRequest<IReadOnlyList<ParentChildSetupInvitationResponse>>;

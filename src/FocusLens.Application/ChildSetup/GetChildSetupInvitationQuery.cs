using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.ChildSetup;

public sealed record GetChildSetupInvitationQuery(string Token)
    : IRequest<Result<ChildSetupInvitationDetailsResponse>>;
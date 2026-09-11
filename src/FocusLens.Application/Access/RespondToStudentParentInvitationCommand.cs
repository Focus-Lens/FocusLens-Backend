using FocusLens.Contracts.Access;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Access;

public sealed record RespondToStudentParentInvitationCommand(
    string Token,
    bool Accept)
    : IRequest<Result<InvitationResponse>>;

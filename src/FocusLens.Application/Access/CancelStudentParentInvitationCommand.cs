using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Access;

public sealed record CancelStudentParentInvitationCommand(Guid InvitationId)
    : IRequest<Result<Success>>;

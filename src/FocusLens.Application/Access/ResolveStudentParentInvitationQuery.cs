using FocusLens.Contracts.Access;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Access;

public sealed record ResolveStudentParentInvitationQuery(string Token)
    : IRequest<Result<ResolveStudentParentInvitationResponse>>;
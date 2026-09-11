using FocusLens.Contracts.Access;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Access;

public sealed record CreateStudentParentInvitationCommand(
    CreateStudentParentInvitationRequest Request)
    : IRequest<Result<StudentParentInvitationResponse>>;

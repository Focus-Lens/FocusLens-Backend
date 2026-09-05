using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Contracts.Access;
using FocusLens.Domain.Access;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Access;

public sealed class AcceptInvitationCommandHandler(
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<AcceptInvitationCommand, Result<InvitationResponse>>
{
    public async Task<Result<InvitationResponse>> Handle(
        AcceptInvitationCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Access.Unauthorized",
                "The current user could not be identified.");
        }

        if (request.InvitationId == Guid.Empty)
        {
            return Error.Validation(
                "Access.InvalidInvitationId",
                "Invitation ID is required.");
        }

        ParentStudentRelationship? relationship =
            await relationshipRepository.FirstOrDefaultAsync(
                relationship => relationship.Id == request.InvitationId,
                relationship => relationship.Student);

        if (relationship is null)
        {
            return Error.NotFound(
                "Access.InvitationNotFound",
                "The invitation was not found.");
        }

        if (relationship.Student.UserId != userId)
        {
            return Error.Forbidden(
                "Access.InvitationNotOwned",
                "You are not allowed to accept this invitation.");
        }

        if (relationship.Status != RelationshipStatus.Pending)
        {
            return Error.Conflict(
                "Access.InvalidInvitationState",
                "Only a pending invitation can be accepted.");
        }

        relationship.Accept();
        relationshipRepository.Update(relationship);
        await unitOfWork.SaveChangesAsync();

        return relationship.ToResponse();
    }
}

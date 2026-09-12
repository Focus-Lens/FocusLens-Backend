using FocusLens.Contracts.Access;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Access;

public sealed class AcceptInvitationCommandHandler(
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider? timeProvider = null)
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

        DateTimeOffset now = (timeProvider ?? TimeProvider.System).GetUtcNow();

        if (relationship.IsExpired(now))
        {
            return Error.Conflict(
                "Access.InvitationExpired",
                "This invitation has expired. Ask the sender to create a new one.");
        }

        relationship.Accept(now);
        relationshipRepository.Update(relationship);
        await unitOfWork.SaveChangesAsync();

        return relationship.ToResponse();
    }
}
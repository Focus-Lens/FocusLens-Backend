using FocusLens.Contracts.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Access;

public sealed class DeclineInvitationCommandHandler(
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    IBaseRepository<StudentParentInvitation> invitationRepository,
    IBaseRepository<Parent> parentRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider? timeProvider = null)
    : IRequestHandler<DeclineInvitationCommand, Result<InvitationResponse>>
{
    public async Task<Result<InvitationResponse>> Handle(
        DeclineInvitationCommand request,
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

        DateTimeOffset now = (timeProvider ?? TimeProvider.System).GetUtcNow();

        ParentStudentRelationship? relationship =
            await relationshipRepository.FirstOrDefaultAsync(
                relationship => relationship.Id == request.InvitationId,
                relationship => relationship.Student,
                relationship => relationship.Parent,
                relationship => relationship.Parent.User);

        if (relationship is not null)
        {
            if (relationship.Student.UserId != userId)
            {
                return Error.Forbidden(
                    "Access.InvitationNotOwned",
                    "You are not allowed to decline this invitation.");
            }

            if (relationship.Status != RelationshipStatus.Pending)
            {
                return Error.Conflict(
                    "Access.InvalidInvitationState",
                    "Only a pending invitation can be declined.");
            }

            if (relationship.IsExpired(now))
            {
                return Error.Conflict(
                    "Access.InvitationExpired",
                    "This invitation has expired. Ask the sender to create a new one.");
            }

            relationship.Reject();
            relationshipRepository.Update(relationship);
            await unitOfWork.SaveChangesAsync();

            return relationship.ToResponse(
                "Incoming",
                relationship.Parent.User?.Email);
        }

        StudentParentInvitation? invitation =
            await invitationRepository.FirstOrDefaultAsync(
                invitation => invitation.Id == request.InvitationId,
                invitation => invitation.Student,
                invitation => invitation.Student.User);

        Parent? parent = await parentRepository.FirstOrDefaultAsync(
            parent => parent.UserId == userId,
            parent => parent.User);

        if (invitation is null || parent is null)
        {
            return Error.NotFound(
                "Access.InvitationNotFound",
                "The invitation was not found.");
        }

        if (invitation.Type == StudentParentInvitationType.Email &&
            (string.IsNullOrWhiteSpace(currentUser.Email) ||
             !string.Equals(
                 invitation.TargetEmailNormalized,
                 currentUser.Email.Trim().ToUpperInvariant(),
                 StringComparison.Ordinal)))
        {
            return Error.Forbidden(
                "Access.InvitationNotOwned",
                "You are not allowed to decline this invitation.");
        }

        if (invitation.Status != ParentInvitationStatus.Pending)
        {
            return Error.Conflict(
                "Access.InvitationNoLongerPending",
                "This invitation is no longer pending.");
        }

        if (invitation.IsExpired(now))
        {
            return Error.Conflict(
                "Access.InvitationInvalidOrExpired",
                "This invitation has expired.");
        }

        invitation.Decline(now);
        invitationRepository.Update(invitation);
        await unitOfWork.SaveChangesAsync();

        return invitation.ToResponse(
            "Incoming",
            parent.Id,
            invitation.Student.User?.Email);
    }
}

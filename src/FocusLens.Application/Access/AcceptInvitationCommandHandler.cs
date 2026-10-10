using FocusLens.Application.Common.Interfaces;
using FocusLens.Contracts.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Notifications;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Access;

public sealed class AcceptInvitationCommandHandler(
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    IBaseRepository<StudentParentInvitation> invitationRepository,
    IBaseRepository<Parent> parentRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider? timeProvider = null,
    INotificationWriter? notificationWriter = null)
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

        DateTimeOffset now = (timeProvider ?? TimeProvider.System).GetUtcNow();

        ParentStudentRelationship? relationship =
            await relationshipRepository.FirstOrDefaultAsync(
                relationship => relationship.Id == request.InvitationId,
                relationship => relationship.Student,
                relationship => relationship.Student.User,
                relationship => relationship.Parent,
                relationship => relationship.Parent.User);

        if (relationship is not null)
        {
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

            if (relationship.IsExpired(now))
            {
                return Error.Conflict(
                    "Access.InvitationExpired",
                    "This invitation has expired. Ask the sender to create a new one.");
            }

            relationship.Accept(now);
            relationshipRepository.Update(relationship);

            if (notificationWriter is not null)
            {
                if (!relationship.Parent.User.IsDisabled &&
                    relationship.Parent.User.DeletedAtUtc is null)
                {
                    await notificationWriter.AddAsync(
                        relationship.Parent.UserId,
                        NotificationAudience.Parent,
                        NotificationCategory.ParentConnectionConfirmed,
                        "Parent connection confirmed",
                        "A student accepted your connection invitation.",
                        $"/parents/students/{relationship.StudentId}/dashboard",
                        "View student",
                        $"relationship:{relationship.Id}:confirmed:parent");
                }

                await notificationWriter.AddAsync(
                    userId,
                    NotificationAudience.Student,
                    NotificationCategory.ParentConnectionConfirmed,
                    "Parent connection confirmed",
                    "Your parent connection is now active.",
                    "/access/parents",
                    "View connection",
                    $"relationship:{relationship.Id}:confirmed:student");
            }

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
                "You are not allowed to accept this invitation.");
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

        ParentStudentRelationship? existingRelationship =
            await relationshipRepository.FirstOrDefaultAsync(item => item.ParentId == parent.Id &&
                                                                     item.StudentId == invitation.StudentId);

        if (existingRelationship is not null &&
            existingRelationship.Status != RelationshipStatus.Revoked)
        {
            return Error.Conflict(
                "Access.RelationshipExists",
                "A pending or active relationship already exists between this parent and student.");
        }

        if (existingRelationship is null)
        {
            relationship = new ParentStudentRelationship(
                parent.Id,
                invitation.StudentId,
                InvitationInitiator.Student,
                invitation.ExpiresAtUtc);

            relationshipRepository.Add(relationship);
        }
        else
        {
            existingRelationship.Reinvite(invitation.ExpiresAtUtc);
            relationshipRepository.Update(existingRelationship);
            relationship = existingRelationship;
        }

        relationship.Accept(now);
        invitation.Accept(now);
        invitationRepository.Update(invitation);

        if (notificationWriter is not null &&
            !invitation.Student.User.IsDisabled &&
            invitation.Student.User.DeletedAtUtc is null)
        {
            await notificationWriter.AddAsync(
                invitation.Student.UserId,
                NotificationAudience.Student,
                NotificationCategory.ParentConnectionConfirmed,
                "Parent connection confirmed",
                "Your invited parent accepted your connection request.",
                "/access/parents",
                "View connection",
                $"relationship:{relationship.Id}:confirmed:student");
        }

        await unitOfWork.SaveChangesAsync();

        return relationship.ToResponse(
            "Incoming",
            invitation.Student.User?.Email);
    }
}
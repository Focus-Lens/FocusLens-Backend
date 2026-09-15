using System.Security.Cryptography;
using System.Text;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Contracts.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Notifications;
using MediatR;

namespace FocusLens.Application.Access;

public sealed class RespondToStudentParentInvitationCommandHandler(
    IBaseRepository<Parent> parentRepository,
        IBaseRepository<StudentParentInvitation> invitationRepository,
        IBaseRepository<ParentStudentRelationship> relationshipRepository,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        INotificationWriter? notificationWriter = null)
    : IRequestHandler<RespondToStudentParentInvitationCommand, Result<InvitationResponse>>
{
    public async Task<Result<InvitationResponse>> Handle(
        RespondToStudentParentInvitationCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty ||
            string.IsNullOrWhiteSpace(currentUser.Email))
        {
            return Error.Unauthorized("Access.Unauthorized", "The current user could not be identified.");
        }

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return InvalidInvitation();
        }

        string tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(request.Token)));
        Parent? parent = await parentRepository.FirstOrDefaultAsync(item => item.UserId == userId);
        StudentParentInvitation? invitation = await invitationRepository.FirstOrDefaultAsync(
            item => item.TokenHash == tokenHash,
            item => item.Student,
            item => item.Student.User);

        if (parent is null || invitation is null)
        {
            return InvalidInvitation();
        }

        if (!string.Equals(
                invitation.TargetEmailNormalized,
                currentUser.Email.Trim().ToUpperInvariant(),
                StringComparison.Ordinal))
        {
            return Error.Forbidden(
                "Access.InvitationEmailMismatch",
                "Sign in with the email address that received this invitation.");
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (invitation.IsExpired(now))
        {
            return InvalidInvitation();
        }

        if (invitation.Status != ParentInvitationStatus.Pending)
        {
            return Error.Conflict(
                "Access.InvitationNoLongerPending",
                "This invitation is no longer pending.");
        }

        if (!request.Accept)
        {
            invitation.Decline(now);
            invitationRepository.Update(invitation);
            await unitOfWork.SaveChangesAsync();

            return new InvitationResponse(
                invitation.Id,
                parent.Id,
                invitation.StudentId,
                invitation.Status.ToString(),
                InvitationInitiator.Student.ToString(),
                invitation.ExpiresAtUtc,
                null,
                null);
        }

        ParentStudentRelationship? relationship = await relationshipRepository.FirstOrDefaultAsync(item =>
            item.ParentId == parent.Id && item.StudentId == invitation.StudentId);

        if (relationship is not null && relationship.Status != RelationshipStatus.Revoked)
        {
            return Error.Conflict(
                "Access.RelationshipExists",
                "A pending or active relationship already exists between this parent and student.");
        }

        if (relationship is null)
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
            relationship.Reinvite(invitation.ExpiresAtUtc);
            relationshipRepository.Update(relationship);
        }

        relationship.Accept(now);
        invitation.Accept(now);
        invitationRepository.Update(invitation);
        if (notificationWriter is not null)
        {
            if (!invitation.Student.User.IsDisabled &&
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
        }

        await unitOfWork.SaveChangesAsync();

        return relationship.ToResponse();
    }

    private static Error InvalidInvitation()
    {
        return Error.NotFound(
            "Access.InvitationInvalidOrExpired",
            "This invitation link is invalid or has expired.");
    }
}

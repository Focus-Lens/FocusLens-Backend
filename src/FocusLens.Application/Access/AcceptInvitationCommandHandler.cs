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

        ParentStudentRelationship? relationship =
            await relationshipRepository.FirstOrDefaultAsync(
            relationship => relationship.Id == request.InvitationId,
                relationship => relationship.Student,
                relationship => relationship.Student.User,
                relationship => relationship.Parent,
                relationship => relationship.Parent.User);

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

        return relationship.ToResponse();
    }
}

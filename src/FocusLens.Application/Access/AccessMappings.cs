using FocusLens.Contracts.Access;
using FocusLens.Domain.Access;

namespace FocusLens.Application.Access;

internal static class AccessMappings
{
    public static InvitationResponse ToResponse(
        this ParentStudentRelationship relationship,
        string direction,
        string? otherPartyEmail = null)
    {
        return new InvitationResponse(
            relationship.Id,
            relationship.ParentId,
            relationship.StudentId,
            relationship.Status.ToString(),
            relationship.InitiatedBy.ToString(),
            direction,
            "Relationship",
            otherPartyEmail,
            relationship.ExpiresAtUtc,
            relationship.RevokedAtUtc);
    }

    public static InvitationResponse ToResponse(
        this StudentParentInvitation invitation,
        string direction,
        Guid? parentId,
        string? otherPartyEmail = null)
    {
        return new InvitationResponse(
            invitation.Id,
            parentId,
            invitation.StudentId,
            invitation.Status.ToString(),
            InvitationInitiator.Student.ToString(),
            direction,
            "StudentParent",
            otherPartyEmail,
            invitation.ExpiresAtUtc,
            null);
    }
}

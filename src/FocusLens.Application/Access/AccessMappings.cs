using FocusLens.Contracts.Access;
using FocusLens.Domain.Access;

namespace FocusLens.Application.Access;

internal static class AccessMappings
{
    public static InvitationResponse ToResponse(
        this ParentStudentRelationship relationship,
        string? parentEmail = null)
        => new(
            relationship.Id,
            relationship.ParentId,
            relationship.StudentId,
            relationship.Status.ToString(),
            relationship.InitiatedBy.ToString(),
            relationship.ExpiresAtUtc,
            relationship.RevokedAtUtc,
            parentEmail);
}

using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;

namespace FocusLens.Application.Parents;

/// <summary>Maintains a parent-owned default dashboard student from active relationships.</summary>
internal static class ActiveChildSelection
{
    public static async Task<bool> ResolveAsync(
        Parent parent,
        IBaseRepository<ParentStudentRelationship> relationshipRepository)
    {
        ParentStudentRelationship[] activeRelationships = (await relationshipRepository.GetAllAsync(
                relationship => relationship.ParentId == parent.Id &&
                                relationship.Status == RelationshipStatus.Active))
            .OrderBy(relationship => relationship.CreatedAtUtc)
            .ThenBy(relationship => relationship.StudentId)
            .ToArray();

        if (parent.ActiveChildId is Guid activeChildId &&
            activeRelationships.Any(relationship => relationship.StudentId == activeChildId))
        {
            return false;
        }

        Guid? replacement = activeRelationships.FirstOrDefault()?.StudentId;
        if (parent.ActiveChildId == replacement)
        {
            return false;
        }

        parent.SetActiveChildId(replacement);
        return true;
    }
}

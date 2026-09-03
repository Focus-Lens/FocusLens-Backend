using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Access;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Access;

public sealed class RevokeRelationshipCommandHandler(
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RevokeRelationshipCommand, Result<Deleted>>
{
    public async Task<Result<Deleted>> Handle(
        RevokeRelationshipCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Access.Unauthorized",
                "The current user could not be identified.");
        }

        if (request.RelationshipId == Guid.Empty)
        {
            return Error.Validation(
                "Access.InvalidRelationshipId",
                "Relationship ID is required.");
        }

        ParentStudentRelationship? relationship =
            await relationshipRepository.FirstOrDefaultAsync(
                relationship => relationship.Id == request.RelationshipId,
                relationship => relationship.Parent,
                relationship => relationship.Student);

        if (relationship is null)
        {
            return Error.NotFound(
                "Access.RelationshipNotFound",
                "The relationship was not found.");
        }

        bool isParent = relationship.Parent.UserId == currentUser.UserId;
        bool isStudent = relationship.Student.UserId == currentUser.UserId;

        if (!isParent && !isStudent)
        {
            return Error.Forbidden(
                "Access.RelationshipNotOwned",
                "You are not allowed to revoke this relationship.");
        }

        if (relationship.Status != RelationshipStatus.Active)
        {
            return Error.Conflict(
                "Access.InvalidRelationshipState",
                "Only an active relationship can be revoked.");
        }

        relationship.Revoke();
        relationshipRepository.Update(relationship);
        await unitOfWork.SaveChangesAsync();

        return Result.Deleted;
    }
}

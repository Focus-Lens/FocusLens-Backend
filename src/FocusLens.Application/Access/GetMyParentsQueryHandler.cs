using FocusLens.Contracts.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Access;

public sealed class GetMyParentsQueryHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    IBaseRepository<Parent> parentRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyParentsQuery, IReadOnlyList<StudentParentSummaryResponse>>
{
    public async Task<IReadOnlyList<StudentParentSummaryResponse>> Handle(
        GetMyParentsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return [];
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(student => student.UserId == userId);

        if (student is null)
        {
            return [];
        }

        IEnumerable<ParentStudentRelationship> relationships =
            await relationshipRepository.GetAllAsync(relationship =>
                relationship.StudentId == student.Id &&
                relationship.Status == RelationshipStatus.Active);

        Guid[] parentIds = relationships
            .Select(relationship => relationship.ParentId)
            .ToArray();

        if (parentIds.Length == 0)
        {
            return [];
        }

        IEnumerable<Parent> parents =
            await parentRepository.GetAllAsync(
                parent => parentIds.Contains(parent.Id),
                parent => parent.User);

        IReadOnlyDictionary<Guid, ParentStudentRelationship> relationshipsByParentId =
            relationships.ToDictionary(relationship => relationship.ParentId);

        return parents
            .Select(parent => new StudentParentSummaryResponse(
                parent.Id,
                parent.User.FirstName,
                parent.User.LastName,
                parent.User.Email,
                relationshipsByParentId[parent.Id].Id,
                relationshipsByParentId[parent.Id].Status.ToString()))
            .ToList();
    }
}
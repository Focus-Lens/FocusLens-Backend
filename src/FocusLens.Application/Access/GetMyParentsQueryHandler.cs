using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Contracts;

using MediatR;

namespace FocusLens.Application.Access;

public sealed class GetMyParentsQueryHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    IBaseRepository<Parent> parentRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyParentsQuery, IReadOnlyList<ParentResponse>>
{
    public async Task<IReadOnlyList<ParentResponse>> Handle(
        GetMyParentsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return [];
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(
            student => student.UserId == userId);

        if (student is null)
        {
            return [];
        }

        IEnumerable<ParentStudentRelationship> relationships =
            await relationshipRepository.GetAllAsync(
                relationship =>
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
                parent => parentIds.Contains(parent.Id));

        return parents
            .Select(parent => new ParentResponse(
                parent.Id,
                parent.UserId))
            .ToList();
    }
}

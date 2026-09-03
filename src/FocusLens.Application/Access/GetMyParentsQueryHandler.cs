using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Contracts.Parents;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Application.Common.Interfaces;
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
        Student? student = await studentRepository.FirstOrDefaultAsync(
            student => student.UserId == currentUser.UserId);

        if (student is null)
        {
            return [];
        }

        IEnumerable<ParentStudentRelationship> relationships =
            await relationshipRepository.GetAllAsync();

        Guid[] parentIds = relationships
            .Where(relationship =>
                relationship.StudentId == student.Id &&
                relationship.Status == RelationshipStatus.Active)
            .Select(relationship => relationship.ParentId)
            .ToArray();

        if (parentIds.Length == 0)
        {
            return [];
        }

        IEnumerable<Parent> parents = await parentRepository.GetAllAsync();

        return parents
            .Where(parent => parentIds.Contains(parent.Id))
            .Select(parent => new ParentResponse(
                parent.Id,
                parent.UserId))
            .ToList();
    }
}

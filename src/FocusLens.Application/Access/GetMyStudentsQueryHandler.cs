using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Application.Common.Interfaces;
using MediatR;

namespace FocusLens.Application.Access;

public sealed class GetMyStudentsQueryHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    IBaseRepository<Student> studentRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyStudentsQuery, IReadOnlyList<StudentResponse>>
{
    public async Task<IReadOnlyList<StudentResponse>> Handle(
        GetMyStudentsQuery request,
        CancellationToken cancellationToken)
    {
        Parent? parent = await parentRepository.FirstOrDefaultAsync(
            parent => parent.UserId == currentUser.UserId);

        if (parent is null)
        {
            return [];
        }

        IEnumerable<ParentStudentRelationship> relationships =
            await relationshipRepository.GetAllAsync();

        Guid[] studentIds = relationships
            .Where(relationship =>
                relationship.ParentId == parent.Id &&
                relationship.Status == RelationshipStatus.Active)
            .Select(relationship => relationship.StudentId)
            .ToArray();

        if (studentIds.Length == 0)
        {
            return [];
        }

        IEnumerable<Student> students = await studentRepository.GetAllAsync();

        return students
            .Where(student => studentIds.Contains(student.Id))
            .Select(student => new StudentResponse(
                student.Id,
                student.UserId))
            .ToList();
    }
}

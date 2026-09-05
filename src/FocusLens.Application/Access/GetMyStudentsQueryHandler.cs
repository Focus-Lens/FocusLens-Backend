using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
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
            await relationshipRepository.GetAllAsync(
                relationship =>
                    relationship.ParentId == parent.Id &&
                    relationship.Status == RelationshipStatus.Active);

        Guid[] studentIds = relationships
            .Select(relationship => relationship.StudentId)
            .ToArray();

        if (studentIds.Length == 0)
        {
            return [];
        }

        IEnumerable<Student> students =
            await studentRepository.GetAllAsync(
                student => studentIds.Contains(student.Id));

        return students
            .Select(student => new StudentResponse(
                student.Id,
                student.UserId,
                student.IsOnboardingCompleted))
            .ToList();
    }
}

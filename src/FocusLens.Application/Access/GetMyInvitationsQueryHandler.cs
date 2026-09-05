using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Contracts.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Application.Common.Interfaces;
using MediatR;

namespace FocusLens.Application.Access;

public sealed class GetMyInvitationsQueryHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<Student> studentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyInvitationsQuery, IReadOnlyList<InvitationResponse>>
{
    public async Task<IReadOnlyList<InvitationResponse>> Handle(
        GetMyInvitationsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return [];
        }

        Parent? parent = await parentRepository.FirstOrDefaultAsync(
            parent => parent.UserId == userId);

        Student? student = await studentRepository.FirstOrDefaultAsync(
            student => student.UserId == userId);

        if (parent is null && student is null)
        {
            return [];
        }

        Guid? parentId = parent?.Id;
        Guid? studentId = student?.Id;

        IEnumerable<ParentStudentRelationship> relationships =
            await relationshipRepository.GetAllAsync(
                relationship =>
                    (parentId.HasValue && relationship.ParentId == parentId.Value) ||
                    (studentId.HasValue && relationship.StudentId == studentId.Value));

        return relationships
            .Select(relationship => relationship.ToResponse())
            .ToList();
    }
}

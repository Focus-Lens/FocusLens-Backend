using FocusLens.Application.Common.Mappings;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Access;

public sealed class GetStudentForParentQueryHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    IBaseRepository<Student> studentRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetStudentForParentQuery, StudentDetailsResponse?>
{
    public async Task<StudentDetailsResponse?> Handle(
        GetStudentForParentQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return null;
        }

        if (request.StudentId == Guid.Empty)
        {
            return null;
        }

        Parent? parent = await parentRepository.FirstOrDefaultAsync(parent => parent.UserId == userId);

        if (parent is null)
        {
            return null;
        }

        ParentStudentRelationship? relationship =
            await relationshipRepository.FirstOrDefaultAsync(relationship =>
                relationship.ParentId == parent.Id &&
                relationship.StudentId == request.StudentId &&
                relationship.Status == RelationshipStatus.Active);

        if (relationship is null)
        {
            return null;
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(
            student => student.Id == request.StudentId,
            student => student.User,
            student => student.Subjects);

        if (student is null)
        {
            return null;
        }

        return student.ToDetailsResponse();
    }
}
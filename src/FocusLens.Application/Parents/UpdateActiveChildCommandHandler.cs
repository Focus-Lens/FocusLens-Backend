using FocusLens.Contracts;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Parents;

public sealed class UpdateActiveChildCommandHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<Student> studentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateActiveChildCommand, Result<ActiveChildResponse>>
{
    public async Task<Result<ActiveChildResponse>> Handle(
        UpdateActiveChildCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized("Parents.CurrentUserUnavailable", "The current user could not be identified.");
        }

        if (request.StudentId == Guid.Empty)
        {
            return Error.Validation("Parents.InvalidActiveChildId", "Student ID is required.");
        }

        Parent? parent = await parentRepository.FirstOrDefaultAsync(parent => parent.UserId == userId);
        if (parent is null)
        {
            return Error.NotFound("Parents.NotFound", "The current user does not have a parent profile.");
        }

        Student? student = await studentRepository.GetByIdAsync(request.StudentId);
        if (student is null)
        {
            return Error.NotFound("Parents.ActiveChildNotFound", "The requested student is not available.");
        }

        ParentStudentRelationship? relationship = await relationshipRepository.FirstOrDefaultAsync(item =>
            item.ParentId == parent.Id && item.StudentId == request.StudentId &&
            item.Status == RelationshipStatus.Active);
        if (relationship is null)
        {
            return Error.NotFound("Parents.ActiveChildNotFound", "The requested student is not available.");
        }

        if (parent.ActiveChildId != student.Id)
        {
            parent.SetActiveChildId(student.Id);
            await unitOfWork.SaveChangesAsync();
        }

        return new ActiveChildResponse(student.Id);
    }
}

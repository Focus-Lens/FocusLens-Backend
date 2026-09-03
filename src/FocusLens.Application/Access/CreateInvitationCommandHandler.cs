using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Contracts.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Access;

public sealed class CreateInvitationCommandHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<Student> studentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateInvitationCommand, Result<InvitationResponse>>
{
    public async Task<Result<InvitationResponse>> Handle(
        CreateInvitationCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Access.Unauthorized",
                "The current user could not be identified.");
        }

        string? studentEmail = request.Request.StudentEmail?.Trim();

        if (string.IsNullOrWhiteSpace(studentEmail))
        {
            return Error.Validation(
                "Access.StudentEmailRequired",
                "Student email is required.");
        }

        Parent? parent = await parentRepository.FirstOrDefaultAsync(
            parent => parent.UserId == currentUser.UserId);

        if (parent is null)
        {
            return Error.NotFound(
                "Access.ParentNotFound",
                "The current user does not have a parent profile.");
        }

        string normalizedEmail = studentEmail.ToUpperInvariant();

        Student? student = await studentRepository.FirstOrDefaultAsync(
            student => student.User.Email != null &&
                       student.User.Email.ToUpper() == normalizedEmail,
            student => student.User);

        if (student is null)
        {
            return Error.NotFound(
                "Access.StudentNotFound",
                "No student was found with the specified email.");
        }

        if (student.UserId == currentUser.UserId)
        {
            return Error.Conflict(
                "Access.SelfInvitation",
                "A user cannot create a parent-student relationship with themselves.");
        }

        ParentStudentRelationship? existingRelationship =
            await relationshipRepository.FirstOrDefaultAsync(
                relationship => relationship.ParentId == parent.Id &&
                                   relationship.StudentId == student.Id);

        if (existingRelationship is not null)
        {
            return Error.Conflict(
                "Access.RelationshipExists",
                "A relationship already exists between this parent and student.");
        }

        ParentStudentRelationship relationship =
            new(parent.Id, student.Id);

        relationshipRepository.Add(relationship);
        await unitOfWork.SaveChangesAsync();

        return new InvitationResponse(
            relationship.Id,
            relationship.ParentId,
            relationship.StudentId,
            relationship.Status.ToString(),
            relationship.RevokedAtUtc);
    }
}

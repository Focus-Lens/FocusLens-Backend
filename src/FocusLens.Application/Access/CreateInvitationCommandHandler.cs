using FocusLens.Application.Common.Interfaces;
using FocusLens.Contracts.Access;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Access;

public sealed class CreateInvitationCommandHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<Student> studentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IEmailSender emailSender,
    TimeProvider? timeProvider = null)
    : IRequestHandler<CreateInvitationCommand, Result<InvitationResponse>>
{
    public async Task<Result<InvitationResponse>> Handle(
        CreateInvitationCommand request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        DateTimeOffset expiresAtUtc = now.AddDays(7);

        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Access.Unauthorized",
                "The current user could not be identified.");
        }

        string? studentEmail = request.Request.Email?.Trim();

        if (string.IsNullOrWhiteSpace(studentEmail))
        {
            return Error.Validation(
                "Access.StudentEmailRequired",
                "Student email is required.");
        }

        Parent? parent = await parentRepository.FirstOrDefaultAsync(parent => parent.UserId == userId);

        if (parent is null)
        {
            return Error.NotFound(
                "Access.ParentNotFound",
                "The current user does not have a parent profile.");
        }

        string? parentEmail = currentUser.Email?.Trim();

        if (string.IsNullOrWhiteSpace(parentEmail))
        {
            return Error.Unauthorized(
                "Access.ParentEmailUnavailable",
                "The parent email address could not be identified.");
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
            await relationshipRepository.FirstOrDefaultAsync(relationship => relationship.ParentId == parent.Id &&
                                                                             relationship.StudentId == student.Id);

        if (existingRelationship is not null)
        {
            if (existingRelationship.Status == RelationshipStatus.Revoked)
            {
                existingRelationship.Reinvite(expiresAtUtc);
                relationshipRepository.Update(existingRelationship);
                await unitOfWork.SaveChangesAsync();

                await emailSender.SendParentStudentInvitationAsync(
                    student.User.Email!,
                    parentEmail,
                    existingRelationship.Id,
                    cancellationToken);

                return existingRelationship.ToResponse("Outgoing", student.User.Email);
            }

            if (existingRelationship.Status == RelationshipStatus.Pending)
            {
                if (existingRelationship.IsExpired(now))
                {
                    existingRelationship.Reinvite(expiresAtUtc);
                    relationshipRepository.Update(existingRelationship);
                    await unitOfWork.SaveChangesAsync();
                }

                await emailSender.SendParentStudentInvitationAsync(
                    student.User.Email!,
                    parentEmail,
                    existingRelationship.Id,
                    cancellationToken);

                return existingRelationship.ToResponse("Outgoing", student.User.Email);
            }

            return Error.Conflict(
                "Access.RelationshipExists",
                "A relationship already exists between this parent and student.");
        }

        ParentStudentRelationship relationship =
            new(parent.Id, student.Id, InvitationInitiator.Parent, expiresAtUtc);

        relationshipRepository.Add(relationship);
        await unitOfWork.SaveChangesAsync();

        await emailSender.SendParentStudentInvitationAsync(
            student.User.Email!,
            parentEmail,
            relationship.Id,
            cancellationToken);

        return relationship.ToResponse("Outgoing", student.User.Email);
    }
}
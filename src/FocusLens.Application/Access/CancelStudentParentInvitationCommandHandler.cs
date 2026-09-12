using FocusLens.Application.Common.Interfaces;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Access;

public sealed class CancelStudentParentInvitationCommandHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudentParentInvitation> invitationRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<CancelStudentParentInvitationCommand, Result<Success>>
{
    public async Task<Result<Success>> Handle(
        CancelStudentParentInvitationCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized("Access.Unauthorized", "The current user could not be identified.");
        }

        if (request.InvitationId == Guid.Empty)
        {
            return Error.Validation("Access.InvalidInvitationId", "Invitation ID is required.");
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(item => item.UserId == userId);
        StudentParentInvitation? invitation =
            await invitationRepository.FirstOrDefaultAsync(item => item.Id == request.InvitationId);

        if (student is null || invitation is null)
        {
            return Error.NotFound("Access.InvitationNotFound", "The invitation was not found.");
        }

        if (invitation.StudentId != student.Id)
        {
            return Error.Forbidden("Access.InvitationNotOwned", "You are not allowed to cancel this invitation.");
        }

        if (invitation.Status != ParentInvitationStatus.Pending || invitation.IsExpired(timeProvider.GetUtcNow()))
        {
            return Error.Conflict("Access.InvitationNoLongerPending",
                "Only a pending invitation can be cancelled.");
        }

        invitation.Cancel(timeProvider.GetUtcNow());
        invitationRepository.Update(invitation);
        await unitOfWork.SaveChangesAsync();
        return Result.Success;
    }
}
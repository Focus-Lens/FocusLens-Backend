using FocusLens.Contracts.Students;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Notifications;
using FocusLens.Domain.Students;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Students;

public sealed class RejectStudyGoalProposalCommandHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudyGoalProposal> proposalRepository,
    INotificationWriter notificationWriter,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<RejectStudyGoalProposalCommand, Result<StudyGoalProposalResponse>>
{
    public async Task<Result<StudyGoalProposalResponse>> Handle(
        RejectStudyGoalProposalCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "StudyGoalProposals.CurrentUserUnavailable",
                "The current user could not be identified.");
        }

        if (request.ProposalId == Guid.Empty)
        {
            return Error.Validation(
                "StudyGoalProposals.InvalidProposalId",
                "Study goal proposal ID is required.");
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(student => student.UserId == userId);
        StudyGoalProposal? proposal =
            await proposalRepository.FirstOrDefaultAsync(
                proposal => proposal.Id == request.ProposalId,
                proposal => proposal.Parent,
                proposal => proposal.Parent.User);

        if (student is null || proposal is null)
        {
            return Error.NotFound(
                "StudyGoalProposals.NotFound",
                "The study goal proposal was not found.");
        }

        if (proposal.StudentId != student.Id)
        {
            return Error.Forbidden(
                "StudyGoalProposals.NotOwnedByStudent",
                "You are not allowed to respond to this study goal proposal.");
        }

        if (proposal.Status != StudyGoalProposalStatus.Pending)
        {
            return Error.Conflict(
                "StudyGoalProposals.NotPending",
                "Only a pending study goal proposal can be rejected.");
        }

        proposal.Reject(timeProvider.GetUtcNow());

        proposalRepository.Update(proposal);
        if (!proposal.Parent.User.IsDisabled && proposal.Parent.User.DeletedAtUtc is null)
        {
            await notificationWriter.AddAsync(
                proposal.Parent.UserId,
                NotificationAudience.Parent,
                NotificationCategory.ParentActivity,
                "Study goal declined",
                "Your proposed study goal was declined.",
                $"/parents/students/{student.Id}/dashboard",
                "View student",
                $"study-goal-proposal:{proposal.Id}:rejected:parent");
        }

        await unitOfWork.SaveChangesAsync();

        return proposal.ToResponse();
    }
}

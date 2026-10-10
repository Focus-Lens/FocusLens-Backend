using FocusLens.Application.Common.Interfaces;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Notifications;
using FocusLens.Domain.Students;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Students;

public sealed class AcceptStudyGoalProposalCommandHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudyGoalProposal> proposalRepository,
    INotificationWriter notificationWriter,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<AcceptStudyGoalProposalCommand, Result<StudyGoalProposalResponse>>
{
    public async Task<Result<StudyGoalProposalResponse>> Handle(
        AcceptStudyGoalProposalCommand request,
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
                "Only a pending study goal proposal can be accepted.");
        }

        Result<StudyTimeGoal> goal = StudyTimeGoal.Create(
            proposal.Goal.Period,
            proposal.Goal.TargetMinutes,
            proposal.Goal.Days,
            proposal.Goal.StartDate);

        if (goal.IsError)
        {
            return goal.Errors;
        }

        proposal.Accept(timeProvider.GetUtcNow());
        student.SetStudyTimeGoal(goal.Value);

        proposalRepository.Update(proposal);
        studentRepository.Update(student);
        if (!proposal.Parent.User.IsDisabled && proposal.Parent.User.DeletedAtUtc is null)
        {
            await notificationWriter.AddAsync(
                proposal.Parent.UserId,
                NotificationAudience.Parent,
                NotificationCategory.ParentActivity,
                "Study goal accepted",
                "Your proposed study goal was accepted.",
                $"/parents/students/{student.Id}/dashboard",
                "View progress",
                $"study-goal-proposal:{proposal.Id}:accepted:parent");
        }

        await unitOfWork.SaveChangesAsync();

        return proposal.ToResponse();
    }
}
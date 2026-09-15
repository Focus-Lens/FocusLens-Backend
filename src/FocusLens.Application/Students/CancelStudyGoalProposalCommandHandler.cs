using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Students;

public sealed class CancelStudyGoalProposalCommandHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<StudyGoalProposal> proposalRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<CancelStudyGoalProposalCommand, Result<Success>>
{
    public async Task<Result<Success>> Handle(
        CancelStudyGoalProposalCommand request,
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

        Parent? parent = await parentRepository.FirstOrDefaultAsync(parent => parent.UserId == userId);
        StudyGoalProposal? proposal =
            await proposalRepository.FirstOrDefaultAsync(proposal => proposal.Id == request.ProposalId);

        if (parent is null || proposal is null)
        {
            return Error.NotFound(
                "StudyGoalProposals.NotFound",
                "The study goal proposal was not found.");
        }

        if (proposal.ParentId != parent.Id)
        {
            return Error.Forbidden(
                "StudyGoalProposals.NotOwnedByParent",
                "You are not allowed to cancel this study goal proposal.");
        }

        if (proposal.Status != StudyGoalProposalStatus.Pending)
        {
            return Error.Conflict(
                "StudyGoalProposals.NotPending",
                "Only a pending study goal proposal can be cancelled.");
        }

        proposal.Cancel(timeProvider.GetUtcNow());

        proposalRepository.Update(proposal);
        await unitOfWork.SaveChangesAsync();

        return Result.Success;
    }
}

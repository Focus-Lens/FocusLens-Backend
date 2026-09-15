using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Students;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Students;

public sealed class GetMyStudyGoalProposalsQueryHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<StudyGoalProposal> proposalRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyStudyGoalProposalsQuery, IReadOnlyList<StudyGoalProposalResponse>>
{
    public async Task<IReadOnlyList<StudyGoalProposalResponse>> Handle(
        GetMyStudyGoalProposalsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return [];
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(student => student.UserId == userId);

        if (student is null)
        {
            return [];
        }

        IEnumerable<StudyGoalProposal> proposals =
            await proposalRepository.GetAllAsync(proposal => proposal.StudentId == student.Id);

        StudyGoalProposal[] proposalList = proposals.ToArray();
        Guid[] parentIds = proposalList
            .Select(proposal => proposal.ParentId)
            .Distinct()
            .ToArray();

        IReadOnlyDictionary<Guid, Parent> parentsById =
            (await parentRepository.GetAllAsync(
                parent => parentIds.Contains(parent.Id),
                parent => parent.User))
            .ToDictionary(parent => parent.Id);

        return proposalList
            .OrderByDescending(proposal => proposal.CreatedAtUtc)
            .Select(proposal =>
            {
                parentsById.TryGetValue(proposal.ParentId, out Parent? parent);
                return proposal.ToResponse(parent?.WeekStartsOn, parent);
            })
            .ToArray();
    }
}

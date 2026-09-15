using FocusLens.Contracts.Students;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Students;

public sealed record CreateStudyGoalProposalCommand(
    Guid StudentId,
    CreateStudyGoalProposalRequest Request)
    : IRequest<Result<StudyGoalProposalResponse>>;

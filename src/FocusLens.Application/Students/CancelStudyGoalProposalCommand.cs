using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Students;

public sealed record CancelStudyGoalProposalCommand(Guid ProposalId)
    : IRequest<Result<Success>>;

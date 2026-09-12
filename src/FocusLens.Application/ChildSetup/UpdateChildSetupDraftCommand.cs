using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.ChildSetup;

public sealed record UpdateChildSetupDraftCommand(
    Guid DraftId,
    UpdateChildSetupDraftRequest Request
) : IRequest<Result<ChildSetupDraftResponse>>;
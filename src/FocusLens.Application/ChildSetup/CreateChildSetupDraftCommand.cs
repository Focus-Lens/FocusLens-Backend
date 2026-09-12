using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.ChildSetup;

public sealed record CreateChildSetupDraftCommand
    : IRequest<Result<ChildSetupDraftResponse>>;
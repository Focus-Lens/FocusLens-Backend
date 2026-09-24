using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.ChildSetup;

public sealed record SetChildSetupProfileModeCommand(
    Guid DraftId,
    string ProfileSetupMode
) : IRequest<Result<bool>>;

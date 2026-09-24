using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.ChildSetup;

public sealed record UpdateChildSetupDraftProfileImageCommand(
    Guid DraftId,
    string FileName,
    long FileSizeBytes,
    Func<CancellationToken, Task<Stream>> OpenReadAsync)
    : IRequest<Result<string>>;

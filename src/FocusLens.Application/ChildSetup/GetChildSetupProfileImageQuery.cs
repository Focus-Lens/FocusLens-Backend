using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.ChildSetup;

public sealed record GetChildSetupProfileImageQuery(Guid DraftId)
    : IRequest<Result<ChildSetupProfileImageFile>>;

public sealed record ChildSetupProfileImageFile(Stream Content, string ContentType);

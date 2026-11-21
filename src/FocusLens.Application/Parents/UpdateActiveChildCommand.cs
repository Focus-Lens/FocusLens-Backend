using FocusLens.Contracts;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Parents;

public sealed record UpdateActiveChildCommand(Guid StudentId)
    : IRequest<Result<ActiveChildResponse>>;

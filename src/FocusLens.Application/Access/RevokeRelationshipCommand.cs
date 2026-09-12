using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Access;

public sealed record RevokeRelationshipCommand(Guid RelationshipId)
    : IRequest<Result<Deleted>>;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Features.Users.Commands.DeleteCurrentUser;

public sealed record DeleteCurrentUserCommand : IRequest<Result<Success>>;
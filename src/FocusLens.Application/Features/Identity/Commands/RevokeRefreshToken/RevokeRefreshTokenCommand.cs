using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.RevokeRefreshToken;

public sealed record RevokeRefreshTokenCommand(
    string RefreshToken) : IRequest<Result<Success>>;
using FocusLens.Application.Features.Identity.Dtos;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.RefreshToken;

public sealed record RefreshTokenCommand(
    string AccessToken,
    string RefreshToken) : IRequest<Result<TokenResponse>>;

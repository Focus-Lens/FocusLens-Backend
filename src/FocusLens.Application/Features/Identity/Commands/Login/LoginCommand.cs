using FocusLens.Application.Features.Identity.Dtos;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.Login;

public sealed record LoginCommand(
    string Email,
    string Password) : IRequest<Result<AuthResponse>>;

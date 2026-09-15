using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Features.Users.Commands.RestoreAccount;

public sealed record RestoreAccountCommand(
    string Email,
    string Password) : IRequest<Result<Success>>;

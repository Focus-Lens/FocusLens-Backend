using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.ResendVerificationCode;

public sealed record ResendVerificationCodeCommand(
    string Email) : IRequest<Result<Success>>;
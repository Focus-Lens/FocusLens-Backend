using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.ForgotPassword;

public sealed record ForgotPasswordCommand(
    string Email) : IRequest<Result<Success>>;
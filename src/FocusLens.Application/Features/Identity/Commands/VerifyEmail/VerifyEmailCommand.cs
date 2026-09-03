using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Features.Identity.Commands.VerifyEmail;

public sealed record VerifyEmailCommand(
    string Email,
    string Otp) : IRequest<Result<Success>>;

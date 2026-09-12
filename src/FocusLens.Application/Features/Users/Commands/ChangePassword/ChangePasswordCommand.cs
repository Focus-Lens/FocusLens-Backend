using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Features.Users.Commands.ChangePassword;

public sealed record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword) : IRequest<Result<Success>>;
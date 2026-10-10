using FocusLens.Application.Features.Users.Commands.ChangePassword;
using FocusLens.Application.Features.Users.Commands.DeleteCurrentUser;
using FocusLens.Application.Features.Users.Commands.UpdateCurrentUser;
using FocusLens.Application.Features.Users.Dtos;
using FocusLens.Application.Features.Users.Queries.GetCurrentUser;
using FocusLens.Application.Features.Users.Queries.GetCurrentUserAccountStatus;
using FocusLens.Domain.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[Authorize]
[Route("api/users")]
public sealed class UsersController : ApiController
{
    private readonly ISender _sender;

    public UsersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(
        CancellationToken cancellationToken)
    {
        Result<UserProfileDto> result = await _sender.Send(
            new GetCurrentUserQuery(),
            cancellationToken);

        return result.Match(
            Ok,
            Problem);
    }

    [HttpGet("me/account-status")]
    public async Task<IActionResult> GetCurrentUserAccountStatus(
        CancellationToken cancellationToken)
    {
        Result<UserAccountStatusDto> result = await _sender.Send(
            new GetCurrentUserAccountStatusQuery(),
            cancellationToken);

        return result.Match(
            Ok,
            Problem);
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateCurrentUser(
        UpdateCurrentUserCommand command,
        CancellationToken cancellationToken)
    {
        Result<UserProfileDto> result = await _sender.Send(command, cancellationToken);

        return result.Match(
            Ok,
            Problem);
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordCommand command,
        CancellationToken cancellationToken)
    {
        Result<Success> result = await _sender.Send(command, cancellationToken);

        return result.Match(
            _ => NoContent(),
            Problem);
    }

    [HttpDelete("me")]
    public async Task<IActionResult> DeleteCurrentUser(CancellationToken cancellationToken)
    {
        Result<Success> result = await _sender.Send(
            new DeleteCurrentUserCommand(),
            cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }
}
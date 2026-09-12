using FocusLens.Application.ChildSetup;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[ApiController]
[Route("api/child-setup-invitations")]
public sealed class ChildSetupInvitationsController(ISender sender)
    : ApiController
{
    [HttpGet("{token}")]
    public async Task<IActionResult> GetInvitation(
        string token,
        CancellationToken cancellationToken)
    {
        Result<ChildSetupInvitationDetailsResponse> result = await sender.Send(
            new GetChildSetupInvitationQuery(token),
            cancellationToken);

        return result.Match(
            Ok,
            Problem);
    }

    [Authorize(Roles = "Student")]
    [HttpPost("{token}/claim")]
    public async Task<IActionResult> ClaimInvitation(
        string token,
        CancellationToken cancellationToken)
    {
        Result<ClaimChildSetupInvitationResponse> result = await sender.Send(
            new ClaimChildSetupInvitationCommand(token),
            cancellationToken);

        return result.Match(
            Ok,
            Problem);
    }
}
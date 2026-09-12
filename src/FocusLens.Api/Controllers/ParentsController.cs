using FocusLens.Application.ChildSetup;
using FocusLens.Application.Parents;
using FocusLens.Contracts;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Domain.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[ApiController]
[Route("api/parents")]
[Authorize(Roles = "Parent")]
public sealed class ParentsController(ISender sender) : ApiController
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        ParentResponse? parent = await sender.Send(new GetMyParentQuery(), cancellationToken);

        return parent is null
            ? NotFound()
            : Ok(parent);
    }

    [HttpPost("child-setups")]
    public async Task<IActionResult> CreateChildSetup(
        CancellationToken cancellationToken)
    {
        Result<ChildSetupDraftResponse> result = await sender.Send(
            new CreateChildSetupDraftCommand(),
            cancellationToken);

        return result.Match(
            draft => StatusCode(StatusCodes.Status201Created, draft),
            Problem);
    }

    [HttpPost("child-setups/{draftId:guid}/invite/resend")]
    public async Task<IActionResult> ResendChildInvitation(
        Guid draftId,
        CancellationToken cancellationToken)
    {
        Result<ChildSetupInvitationResponse> result = await sender.Send(
            new ResendChildSetupInvitationCommand(draftId),
            cancellationToken);

        return result.Match(
            Ok,
            Problem);
    }

    [HttpGet("child-setups/{draftId:guid}")]
    public async Task<IActionResult> GetChildSetup(
        Guid draftId,
        CancellationToken cancellationToken)
    {
        Result<ChildSetupDraftResponse> result = await sender.Send(
            new GetChildSetupDraftQuery(draftId),
            cancellationToken);

        return result.Match(
            Ok,
            Problem);
    }

    [HttpPut("child-setups/{draftId:guid}")]
    public async Task<IActionResult> UpdateChildSetup(
        Guid draftId,
        UpdateChildSetupDraftRequest request,
        CancellationToken cancellationToken)
    {
        Result<ChildSetupDraftResponse> result = await sender.Send(
            new UpdateChildSetupDraftCommand(draftId, request),
            cancellationToken);

        return result.Match(
            Ok,
            Problem);
    }

    [HttpPost("child-setups/{draftId:guid}/invite")]
    public async Task<IActionResult> InviteChild(
        Guid draftId,
        CreateChildSetupInvitationRequest request,
        CancellationToken cancellationToken)
    {
        Result<ChildSetupInvitationResponse> result = await sender.Send(
            new CreateChildSetupInvitationCommand(draftId, request),
            cancellationToken);

        return result.Match(
            Ok,
            Problem);
    }
}
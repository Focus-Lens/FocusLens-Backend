using FocusLens.Application.Access;
using FocusLens.Contracts.Access;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[ApiController]
[Route("api/access")]
public sealed class AccessController(ISender sender) : ControllerBase
{
    [HttpPost("invitations")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> CreateInvitation(
        [FromBody] CreateInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateInvitationCommand(request),
            cancellationToken);

        if (result.IsSuccess)
        {
            return StatusCode(StatusCodes.Status201Created, result.Value);
        }

        return result.TopError.Type switch
        {
            FocusLens.Domain.Common.Results.ErrorKind.Validation => BadRequest(result.Errors),
            FocusLens.Domain.Common.Results.ErrorKind.Unauthorized => Unauthorized(result.Errors),
            FocusLens.Domain.Common.Results.ErrorKind.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result.Errors),
            FocusLens.Domain.Common.Results.ErrorKind.NotFound => NotFound(result.Errors),
            FocusLens.Domain.Common.Results.ErrorKind.Conflict => Conflict(result.Errors),
            _ => StatusCode(StatusCodes.Status500InternalServerError, result.Errors)
        };
    }

    [HttpGet("invitations")]
    [Authorize(Roles = "Parent,Student")]
    public async Task<IActionResult> GetMyInvitations(
        CancellationToken cancellationToken)
    {
        var invitations = await sender.Send(
            new GetMyInvitationsQuery(),
            cancellationToken);

        return Ok(invitations);
    }

    [HttpPost("invitations/{invitationId:guid}/accept")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> AcceptInvitation(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AcceptInvitationCommand(invitationId),
            cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return result.TopError.Type switch
        {
            FocusLens.Domain.Common.Results.ErrorKind.Validation => BadRequest(result.Errors),
            FocusLens.Domain.Common.Results.ErrorKind.Unauthorized => Unauthorized(result.Errors),
            FocusLens.Domain.Common.Results.ErrorKind.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result.Errors),
            FocusLens.Domain.Common.Results.ErrorKind.NotFound => NotFound(result.Errors),
            FocusLens.Domain.Common.Results.ErrorKind.Conflict => Conflict(result.Errors),
            _ => StatusCode(StatusCodes.Status500InternalServerError, result.Errors)
        };
    }

    [HttpPost("invitations/{invitationId:guid}/reject")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> RejectInvitation(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RejectInvitationCommand(invitationId),
            cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return result.TopError.Type switch
        {
            FocusLens.Domain.Common.Results.ErrorKind.Validation => BadRequest(result.Errors),
            FocusLens.Domain.Common.Results.ErrorKind.Unauthorized => Unauthorized(result.Errors),
            FocusLens.Domain.Common.Results.ErrorKind.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result.Errors),
            FocusLens.Domain.Common.Results.ErrorKind.NotFound => NotFound(result.Errors),
            FocusLens.Domain.Common.Results.ErrorKind.Conflict => Conflict(result.Errors),
            _ => StatusCode(StatusCodes.Status500InternalServerError, result.Errors)
        };
    }

    [HttpGet("students")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> GetMyStudents(
        CancellationToken cancellationToken)
    {
        var students = await sender.Send(
            new GetMyStudentsQuery(),
            cancellationToken);

        return Ok(students);
    }

    [HttpGet("parents")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyParents(
        CancellationToken cancellationToken)
    {
        var parents = await sender.Send(
            new GetMyParentsQuery(),
            cancellationToken);

        return Ok(parents);
    }
    [HttpDelete("{relationshipId:guid}")]
    [Authorize(Roles = "Parent,Student")]
    public async Task<IActionResult> RevokeRelationship(
        Guid relationshipId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RevokeRelationshipCommand(relationshipId),
            cancellationToken);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return result.TopError.Type switch
        {
            FocusLens.Domain.Common.Results.ErrorKind.Validation => BadRequest(result.Errors),
            FocusLens.Domain.Common.Results.ErrorKind.Unauthorized => Unauthorized(result.Errors),
            FocusLens.Domain.Common.Results.ErrorKind.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result.Errors),
            FocusLens.Domain.Common.Results.ErrorKind.NotFound => NotFound(result.Errors),
            FocusLens.Domain.Common.Results.ErrorKind.Conflict => Conflict(result.Errors),
            _ => StatusCode(StatusCodes.Status500InternalServerError, result.Errors)
        };
    }
}

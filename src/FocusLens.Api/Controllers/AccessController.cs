using FocusLens.Application.Access;
using FocusLens.Contracts.Access;
using FocusLens.Contracts.Students;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[ApiController]
[Route("api/access")]
public sealed class AccessController(ISender sender) : ApiController
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

        return Problem(result.Errors);
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

        return Problem(result.Errors);
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

        return Problem(result.Errors);
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

    [HttpGet("students/{studentId:guid}")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> GetStudent(
        Guid studentId,
        CancellationToken cancellationToken)
    {
        StudentDetailsResponse? student = await sender.Send(
            new GetStudentForParentQuery(studentId),
            cancellationToken);

        return student is null
            ? NotFound()
            : Ok(student);
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

        return Problem(result.Errors);
    }
}

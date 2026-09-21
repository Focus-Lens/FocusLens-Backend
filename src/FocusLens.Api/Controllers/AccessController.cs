using FocusLens.Application.Access;
using FocusLens.Contracts.Access;
using FocusLens.Contracts.Students;
using FocusLens.Domain.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[ApiController]
[Route("api/access")]
public sealed class AccessController(ISender sender) : ApiController
{
    [HttpPost("invitations")]
    [Authorize(Roles = "Parent,Student")]
    public async Task<IActionResult> CreateInvitation(
        [FromBody] CreateInvitationRequest request,
        CancellationToken cancellationToken)
    {
        Result<InvitationResponse> result;

        if (User.IsInRole("Parent"))
        {
            result = await sender.Send(
                new CreateInvitationCommand(request),
                cancellationToken);
        }
        else if (User.IsInRole("Student"))
        {
            result = await sender.Send(
                new CreateStudentParentInvitationCommand(request),
                cancellationToken);
        }
        else
        {
            return Forbid();
        }

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : Problem(result.Errors);
    }

    [HttpGet("invitations/resolve")]
    [AllowAnonymous]
    public async Task<IActionResult> ResolveStudentParentInvitation(
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        Result<ResolveStudentParentInvitationResponse> result = await sender.Send(
            new ResolveStudentParentInvitationQuery(token),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : Problem(result.Errors);
    }

    [HttpPost("invitations/{invitationId:guid}/accept")]
    [Authorize(Roles = "Parent,Student")]
    public async Task<IActionResult> AcceptInvitation(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        Result<InvitationResponse> result = await sender.Send(
            new AcceptInvitationCommand(invitationId),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : Problem(result.Errors);
    }

    [HttpPost("invitations/{invitationId:guid}/decline")]
    [Authorize(Roles = "Parent,Student")]
    public async Task<IActionResult> DeclineInvitation(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        Result<InvitationResponse> result = await sender.Send(
            new DeclineInvitationCommand(invitationId),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : Problem(result.Errors);
    }

    [HttpPost("invitations/{invitationId:guid}/cancel")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> CancelInvitation(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        Result<Success> result = await sender.Send(
            new CancelStudentParentInvitationCommand(invitationId),
            cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : Problem(result.Errors);
    }

    [HttpGet("invitations")]
    [Authorize(Roles = "Parent,Student")]
    public async Task<IActionResult> GetMyInvitations(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<InvitationResponse> invitations = await sender.Send(
            new GetMyInvitationsQuery(),
            cancellationToken);

        return Ok(invitations);
    }

    [HttpGet("students")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> GetMyStudents(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ParentStudentSummaryResponse> students = await sender.Send(
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
        IReadOnlyList<StudentParentSummaryResponse> parents = await sender.Send(
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
        Result<Deleted> result = await sender.Send(
            new RevokeRelationshipCommand(relationshipId),
            cancellationToken);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return Problem(result.Errors);
    }
}
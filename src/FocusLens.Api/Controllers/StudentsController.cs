using FocusLens.Application.ChildSetup;
using FocusLens.Application.Features.Identity.Dtos;
using FocusLens.Application.Students;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Contracts.Students;
using FocusLens.Domain.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[ApiController]
[Route("api/students")]
[Authorize(Roles = "Student")]
public sealed class StudentsController(ISender sender) : ApiController
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        StudentDetailsResponse? student = await sender.Send(new GetMyStudentQuery(), cancellationToken);

        return student is null ? NotFound() : Ok(student);
    }

    [HttpGet("me/child-setup")]
    public async Task<IActionResult> GetMyChildSetup(
        CancellationToken cancellationToken)
    {
        Result<ChildSetupDraftResponse> result = await sender.Send(
            new GetMyClaimedChildSetupQuery(),
            cancellationToken);

        return result.Match(
            Ok,
            Problem);
    }


    [HttpPost("onboarding")]
    public async Task<IActionResult> CompleteOnboarding(
        [FromBody] CompleteStudentOnboardingRequest request,
        CancellationToken cancellationToken
    )
    {
        Result<AuthResponse> result = await sender.Send(
            new CompleteStudentOnboardingCommand(request),
            cancellationToken
        );

        return result.Match(Ok, Problem);
    }

    [HttpPatch("me/preferences")]
    public async Task<IActionResult> UpdatePreferences(
        [FromBody] UpdateStudentPreferencesRequest request,
        CancellationToken cancellationToken
    )
    {
        Result<StudentDetailsResponse> result = await sender.Send(
            new UpdateStudentPreferencesCommand(request),
            cancellationToken
        );

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(result.Errors);
    }

    [HttpGet("me/profile-image")]
    public async Task<IActionResult> GetProfileImage(CancellationToken cancellationToken)
    {
        Result<StudentProfileImageFile> result = await sender.Send(
            new GetStudentProfileImageQuery(),
            cancellationToken);

        return result.Match(
            image => File(image.Content, image.ContentType),
            Problem);
    }

    [HttpPut("me/profile-image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UpdateProfileImage(
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return Problem([
                Error.Validation("Students.EmptyProfileImage", "The profile image is empty.")
            ]);
        }

        Result<StudentDetailsResponse> result = await sender.Send(
            new UpdateStudentProfileImageCommand(
                file.FileName,
                file.Length,
                cancellation => Task.FromResult<Stream>(file.OpenReadStream())),
            cancellationToken);

        return result.Match(Ok, Problem);
    }

    [HttpDelete("me/profile-image")]
    public async Task<IActionResult> RemoveProfileImage(CancellationToken cancellationToken)
    {
        Result<StudentDetailsResponse> result = await sender.Send(
            new RemoveStudentProfileImageCommand(),
            cancellationToken);

        return result.Match(Ok, Problem);
    }

    [HttpDelete("me/study-history")]
    public async Task<IActionResult> DeleteStudyHistory(CancellationToken cancellationToken)
    {
        Result<Success> result = await sender.Send(
            new DeleteStudyHistoryCommand(),
            cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }

    [HttpGet("me/data/export")]
    public async Task<IActionResult> ExportData(CancellationToken cancellationToken)
    {
        StudentDataExport export = await sender.Send(
            new ExportStudentDataQuery(),
            cancellationToken);

        return File(export.Content, export.ContentType, export.FileName);
    }

    [HttpGet("me/study-goal-proposals")]
    public async Task<IActionResult> GetMyStudyGoalProposals(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<StudyGoalProposalResponse> proposals = await sender.Send(
            new GetMyStudyGoalProposalsQuery(),
            cancellationToken);

        return Ok(proposals);
    }

    [HttpPost("me/study-goal-proposals/{proposalId:guid}/accept")]
    public async Task<IActionResult> AcceptStudyGoalProposal(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        Result<StudyGoalProposalResponse> result = await sender.Send(
            new AcceptStudyGoalProposalCommand(proposalId),
            cancellationToken);

        return result.Match(
            Ok,
            Problem);
    }

    [HttpPost("me/study-goal-proposals/{proposalId:guid}/reject")]
    public async Task<IActionResult> RejectStudyGoalProposal(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        Result<StudyGoalProposalResponse> result = await sender.Send(
            new RejectStudyGoalProposalCommand(proposalId),
            cancellationToken);

        return result.Match(
            Ok,
            Problem);
    }

    [HttpPost("me/child-setup/activate")]
    public async Task<IActionResult> ActivateChildSetup(
        CancellationToken cancellationToken)
    {
        Result<StudentDetailsResponse> result = await sender.Send(
            new ActivateChildSetupCommand(),
            cancellationToken);

        return result.Match(
            Ok,
            Problem);
    }
}

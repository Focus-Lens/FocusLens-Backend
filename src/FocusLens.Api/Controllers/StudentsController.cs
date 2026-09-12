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
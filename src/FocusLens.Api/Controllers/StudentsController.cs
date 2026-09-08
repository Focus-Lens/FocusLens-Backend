using FocusLens.Application.Students;
using FocusLens.Contracts.Students;
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
        var student = await sender.Send(
            new GetMyStudentQuery(),
            cancellationToken);

        return student is null
            ? NotFound()
            : Ok(student);
    }

    [HttpPost("onboarding")]
    public async Task<IActionResult> CompleteOnboarding(
        [FromBody] CompleteStudentOnboardingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CompleteStudentOnboardingCommand(request),
            cancellationToken);

        return result.Match(
            Ok,
            Problem);
    }

    [HttpPatch("me/preferences")]
    public async Task<IActionResult> UpdatePreferences(
        [FromBody] UpdateStudentPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateStudentPreferencesCommand(request),
            cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(result.Errors);
    }

    [HttpPost("me/subjects")]
    public async Task<IActionResult> CreateCustomSubject(
        [FromBody] CreateCustomStudentSubjectRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateCustomStudentSubjectCommand(request),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : Problem(result.Errors);
    }
}

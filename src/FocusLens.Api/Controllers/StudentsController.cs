using FocusLens.Application.Students;
using FocusLens.Contracts.Students;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[ApiController]
[Route("api/students")]
[Authorize(Roles = "Student")]
public sealed class StudentsController(ISender sender) : ControllerBase
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

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return result.TopError.Type switch
        {
            FocusLens.Domain.Common.Results.ErrorKind.Validation =>
                BadRequest(result.Errors),

            FocusLens.Domain.Common.Results.ErrorKind.Unauthorized =>
                Unauthorized(result.Errors),

            FocusLens.Domain.Common.Results.ErrorKind.Forbidden =>
                StatusCode(StatusCodes.Status403Forbidden, result.Errors),

            FocusLens.Domain.Common.Results.ErrorKind.NotFound =>
                NotFound(result.Errors),

            FocusLens.Domain.Common.Results.ErrorKind.Conflict =>
                Conflict(result.Errors),

            _ =>
                StatusCode(
                    StatusCodes.Status500InternalServerError,
                    result.Errors)
        };
    }
}

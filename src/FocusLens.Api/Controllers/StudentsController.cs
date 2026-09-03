using FocusLens.Application.Students;
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
        var student = await sender.Send(new GetMyStudentQuery(), cancellationToken);

        return student is null
            ? NotFound()
            : Ok(student);
    }
}

using FocusLens.Application.Students;
using FocusLens.Contracts.Students;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[ApiController]
[Route("api/students/me/subjects")]
[Authorize(Roles = "Student")]
public sealed class StudentSubjectsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetMyStudentSubjectsQuery(), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> CreateCustom(
        [FromBody] CreateCustomStudentSubjectRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateCustomStudentSubjectCommand(request),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : Problem(result.Errors);
    }
}

using FocusLens.Application.Parents;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[ApiController]
[Route("api/parents")]
[Authorize(Roles = "Parent")]
public sealed class ParentsController(ISender sender) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var parent = await sender.Send(new GetMyParentQuery(), cancellationToken);

        return parent is null
            ? NotFound()
            : Ok(parent);
    }
}

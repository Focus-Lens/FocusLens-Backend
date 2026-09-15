using FocusLens.Application.Progress;
using FocusLens.Contracts.Progress;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[ApiController]
[Route("api/progress")]
[Authorize(Roles = "Student,Parent")]
public sealed class ProgressController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] Guid? studentId,
        [FromQuery] string? range,
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        CancellationToken cancellationToken)
    {
        if (!HasValidRange(range, dateFrom, dateTo))
        {
            return BadRequest(new { error = "The requested progress date range is invalid." });
        }

        ProgressResponse? response = await sender.Send(
            new GetProgressQuery(studentId, range, dateFrom, dateTo), cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("behavioral")]
    public async Task<IActionResult> GetBehavioral(
        [FromQuery] Guid? studentId,
        [FromQuery] string? range,
        CancellationToken cancellationToken)
    {
        BehavioralProgressResponse? response = await sender.Send(
            new GetBehavioralProgressQuery(studentId, range), cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    private static bool HasValidRange(string? range, DateOnly? dateFrom, DateOnly? dateTo)
    {
        string value = string.IsNullOrWhiteSpace(range) ? "Last30Days" : range.Trim();
        return value.Equals("Last7Days", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Last30Days", StringComparison.OrdinalIgnoreCase) ||
               (value.Equals("Custom", StringComparison.OrdinalIgnoreCase) &&
                dateFrom is not null &&
                dateTo is not null &&
                dateFrom <= dateTo);
    }
}

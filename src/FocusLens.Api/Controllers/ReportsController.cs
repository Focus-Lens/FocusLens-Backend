using FocusLens.Application.Reports;
using FocusLens.Contracts.Reports;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Student,Parent")]
public sealed class ReportsController(ISender sender) : ControllerBase
{
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(
        [FromQuery] Guid? studentId,
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        [FromQuery] Guid? subjectId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        ReportSessionListResponse? response = await sender.Send(
            new GetReportSessionsQuery(studentId, dateFrom, dateTo, subjectId, status, page, pageSize),
            cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("sessions/{sessionId:guid}")]
    public async Task<IActionResult> GetSession(
        Guid sessionId,
        [FromQuery] Guid? studentId,
        CancellationToken cancellationToken)
    {
        ReportSessionDetailResponse? response = await sender.Send(
            new GetReportSessionDetailQuery(sessionId, studentId),
            cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }
}

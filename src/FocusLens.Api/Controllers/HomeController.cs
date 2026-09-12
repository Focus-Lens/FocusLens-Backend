using FocusLens.Application.Home;
using FocusLens.Contracts.Home;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[ApiController]
[Route("api/home")]
[Authorize(Roles = "Student")]
public sealed class HomeController(ISender sender) : ControllerBase
{
    [HttpGet("study-overview")]
    public async Task<IActionResult> GetStudyOverview(CancellationToken cancellationToken)
    {
        StudyOverviewResponse? response = await sender.Send(new GetStudyOverviewQuery(), cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("streak")]
    public async Task<IActionResult> GetStreak(CancellationToken cancellationToken)
    {
        StudyStreakResponse? response = await sender.Send(new GetStudyStreakQuery(), cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("sessions-by-day")]
    public async Task<IActionResult> GetSessionsByDay(CancellationToken cancellationToken)
    {
        SessionsByDayResponse? response = await sender.Send(new GetSessionsByDayQuery(), cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }
}
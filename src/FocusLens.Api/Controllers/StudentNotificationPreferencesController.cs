using FocusLens.Application.Notifications;
using FocusLens.Contracts.Notifications;
using FocusLens.Domain.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[ApiController]
[Route("api/students/me/notification-preferences")]
[Authorize(Roles = "Student")]
public sealed class StudentNotificationPreferencesController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        StudentNotificationPreferencesResponse preferences = await sender.Send(
            new GetMyStudentNotificationPreferencesQuery(),
            cancellationToken);

        return Ok(preferences);
    }

    [HttpPatch]
    public async Task<IActionResult> Update(
        UpdateStudentNotificationPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        Result<StudentNotificationPreferencesResponse> result = await sender.Send(
            new UpdateStudentNotificationPreferencesCommand(request),
            cancellationToken);

        return result.Match(Ok, Problem);
    }
}
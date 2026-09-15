using FocusLens.Application.Notifications;
using FocusLens.Contracts.Notifications;
using FocusLens.Domain.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> GetMine(
        [FromQuery] NotificationFilter filter = NotificationFilter.All,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<NotificationResponse> notifications = await sender.Send(
            new GetMyNotificationsQuery(filter),
            cancellationToken);

        return Ok(notifications);
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        Result<NotificationResponse> result = await sender.Send(
            new MarkNotificationReadCommand(notificationId),
            cancellationToken);

        return result.Match(Ok, Problem);
    }

    [HttpPost("device-tokens")]
    public async Task<IActionResult> RegisterDeviceToken(
        RegisterDeviceTokenRequest request,
        CancellationToken cancellationToken)
    {
        Result<DeviceTokenResponse> result = await sender.Send(
            new RegisterDeviceTokenCommand(request),
            cancellationToken);

        return result.Match(Ok, Problem);
    }

    [HttpDelete("device-tokens/{deviceTokenId:guid}")]
    public async Task<IActionResult> RemoveDeviceToken(
        Guid deviceTokenId,
        CancellationToken cancellationToken)
    {
        Result<Success> result = await sender.Send(
            new RemoveDeviceTokenCommand(deviceTokenId),
            cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }
}

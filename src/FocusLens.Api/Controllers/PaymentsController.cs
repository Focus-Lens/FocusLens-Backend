using System.Text.Json;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Payments;
using FocusLens.Contracts.Payments;
using FocusLens.Domain.Common.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[Route("api/payments")]
[Authorize(Roles = "Student,Parent")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PaymentsController(IPayments payments, ICurrentUser currentUser) : ApiController
{
    [HttpPost("checkouts")]
    public async Task<ActionResult<CheckoutResponse>> Start(
        StartCheckoutRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid buyerId)
        {
            return Unauthorized();
        }

        Result<CheckoutResponse> result =
            await payments.StartAsync(buyerId, User.IsInRole("Parent"), request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Errors);
    }

    [HttpGet("checkouts/{checkoutId:guid}")]
    public async Task<ActionResult<CheckoutResponse>> Get(Guid checkoutId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid buyerId)
        {
            return Unauthorized();
        }

        CheckoutResponse? checkout = await payments.GetAsync(buyerId, checkoutId, cancellationToken);
        return checkout is null ? NotFound() : Ok(checkout);
    }

    [AllowAnonymous]
    [HttpPost("paymob/webhook/{mode}")]
    public async Task<IActionResult> PaymobWebhook(
        string mode, [FromBody] JsonElement callback, [FromQuery] string? hmac, CancellationToken cancellationToken)
    {
        if (mode is not ("sandbox" or "live"))
        {
            return NotFound();
        }

        bool accepted = await payments.ProcessCallbackAsync(mode == "sandbox", callback, hmac, cancellationToken);
        return accepted ? NoContent() : BadRequest();
    }
}

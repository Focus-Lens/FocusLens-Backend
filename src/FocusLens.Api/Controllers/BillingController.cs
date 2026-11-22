using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Payments;
using FocusLens.Contracts.Payments;
using FocusLens.Domain.Common.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[Route("api/payments/billing")]
[Authorize(Roles = "Parent")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BillingController(IBilling billing, ICurrentUser currentUser) : ApiController
{
    [HttpGet]
    public Task<ActionResult<BillingOverview>> Get(CancellationToken cancellationToken) =>
        ForBuyer(id => billing.GetAsync(id, cancellationToken));

    [HttpPut("contact")]
    public Task<ActionResult<BillingContactResponse>> UpdateContact(
        UpdateBillingContactRequest request, CancellationToken cancellationToken) =>
        ForBuyer(id => billing.UpdateContactAsync(id, request, cancellationToken));

    [HttpPut("cancellation")]
    public Task<ActionResult<BillingOverview>> ScheduleCancellation(
        ScheduleCancellationRequest request, CancellationToken cancellationToken) =>
        ForBuyer(id => billing.ScheduleCancellationAsync(id, request, cancellationToken));

    [HttpDelete("cancellation/{checkoutId:guid}")]
    public Task<ActionResult<BillingOverview>> Resume(Guid checkoutId, CancellationToken cancellationToken) =>
        ForBuyer(id => billing.ResumeAsync(id, checkoutId, cancellationToken));

    private async Task<ActionResult<T>> ForBuyer<T>(Func<Guid, Task<Result<T>>> action)
    {
        if (currentUser.UserId is not Guid buyerId)
        {
            return Unauthorized();
        }

        Result<T> result = await action(buyerId);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Errors);
    }
}

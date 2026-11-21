using System.Text.Json;
using FocusLens.Contracts.Payments;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Application.Payments;

public interface IPayments
{
    Task<Result<CheckoutResponse>> StartAsync(Guid buyerId, bool isParent, StartCheckoutRequest request,
        CancellationToken cancellationToken);

    Task<CheckoutResponse?> GetAsync(Guid buyerId, Guid checkoutId, CancellationToken cancellationToken);

    Task<bool> ProcessCallbackAsync(bool isSandbox, JsonElement callback, string? signature,
        CancellationToken cancellationToken);
}

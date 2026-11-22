using FocusLens.Contracts.Payments;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Application.Payments;

public interface IBilling
{
    Task<Result<BillingOverview>> GetAsync(Guid buyerId, CancellationToken cancellationToken);
    Task<Result<BillingOverview>> ScheduleCancellationAsync(Guid buyerId, ScheduleCancellationRequest request,
        CancellationToken cancellationToken);
    Task<Result<BillingOverview>> ResumeAsync(Guid buyerId, Guid checkoutId, CancellationToken cancellationToken);
    Task<Result<BillingContactResponse>> UpdateContactAsync(Guid buyerId, UpdateBillingContactRequest request,
        CancellationToken cancellationToken);
}

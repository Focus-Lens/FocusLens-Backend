using FocusLens.Contracts.Payments;

namespace FocusLens.Infrastructure.Payments;

internal sealed class PaymentPurchase
{
    public const string Currency = "EGP";
    public static readonly TimeSpan CheckoutLifetime = TimeSpan.FromHours(1);

    public Guid Id { get; init; }
    public Guid BuyerId { get; init; }
    public bool IsSandbox { get; init; }
    public PaidPlan Plan { get; init; }
    public BillingTerm Term { get; init; }
    public PaymentMethod Method { get; init; }
    public Guid[] StudentIds { get; init; } = [];
    public int AmountPiasters { get; init; }
    public int IntegrationId { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public PaymentStatus Status { get; set; }
    public long? ProviderOrderId { get; set; }
    public long? PaidTransactionId { get; private set; }
    public string? CheckoutUrl { get; set; }
    public DateTimeOffset? PaidAtUtc { get; private set; }
    public DateTimeOffset? PaidThroughUtc { get; private set; }

    public void RecordPayment(long transactionId, DateTimeOffset paidAt)
    {
        Status = PaymentStatus.Paid;
        PaidTransactionId = transactionId;
        PaidAtUtc = paidAt;
        PaidThroughUtc = Term == BillingTerm.Monthly ? paidAt.AddMonths(1) : paidAt.AddYears(1);
        CheckoutUrl = null;
    }

    public CheckoutResponse ToResponse(DateTimeOffset now) =>
        new(Id, Plan, Term, Method, StudentIds, AmountPiasters, Currency, Status,
            Status == PaymentStatus.Pending && now < CreatedAtUtc + CheckoutLifetime
                ? CheckoutUrl
                : null,
            PaidThroughUtc, IsSandbox);
}

namespace FocusLens.Contracts.Payments;

public enum PaidPlan { Plus = 1, Family }
public enum BillingTerm { Monthly = 1, Yearly }
public enum PaymentMethod { Card = 1, MobileWallet }
public enum PaymentStatus { Pending, Paid, Failed }

public sealed record StartCheckoutRequest(
    Guid CheckoutId,
    PaidPlan Plan,
    BillingTerm Term,
    PaymentMethod Method,
    Guid[]? StudentIds,
    string? BillingPhoneNumber);

public sealed record CheckoutResponse(
    Guid CheckoutId,
    PaidPlan Plan,
    BillingTerm Term,
    PaymentMethod Method,
    Guid[] StudentIds,
    int AmountPiasters,
    string Currency,
    PaymentStatus Status,
    string? CheckoutUrl,
    DateTimeOffset? PaidThroughUtc,
    bool IsSandbox);

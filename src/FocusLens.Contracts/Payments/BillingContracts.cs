namespace FocusLens.Contracts.Payments;

public enum BillingStatus { Free, Active, CancellationScheduled, RenewalDue, PaymentFailed }

public sealed record BillingContactResponse(string FullName, string Email, string? City, string? Country, string? Address);

public sealed record UpdateBillingContactRequest(string? FullName, string? Email, string? City, string? Country, string? Address);

public sealed record ScheduleCancellationRequest(Guid CheckoutId, Guid? KeepStudentId);

public sealed record BillingChild(Guid StudentId, string Name, bool IsPaused, int? CompletedSessions);

public sealed record BillingPayment(
    Guid CheckoutId, PaidPlan Plan, BillingTerm Term, PaymentMethod Method,
    int AmountPiasters, string Currency, PaymentStatus Status,
    DateTimeOffset? PaidAtUtc, DateTimeOffset? PaidThroughUtc, string? CardBrand, string? CardLastFour, Guid[] StudentIds);

public sealed record BillingOverview(
    BillingStatus Status, bool IsSandbox, BillingPayment? LastPayment, BillingPayment? OpenCheckout,
    BillingContactResponse Contact, BillingChild[] Children, Guid[] BeneficiaryIds,
    DateTimeOffset? CancellationEffectiveAtUtc, Guid? KeepStudentId,
    DateTimeOffset? UsageFromUtc, DateTimeOffset? UsageToUtc, bool CardAvailable, bool WalletAvailable);

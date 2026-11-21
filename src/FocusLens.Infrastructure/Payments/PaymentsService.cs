using System.Text.Json;
using FocusLens.Application.Payments;
using FocusLens.Contracts.Payments;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FocusLens.Infrastructure.Payments;

internal sealed class PaymentsService(
    ApplicationDbContext db,
    PaymobClient paymob,
    IOptions<PaymobOptions> options,
    TimeProvider clock,
    ILogger<PaymentsService> logger) : IPayments
{
    public async Task<Result<CheckoutResponse>> StartAsync(Guid buyerId, bool isParent, StartCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        Guid[] requestedStudents = (request.StudentIds ?? []).Order().ToArray();
        if (request.CheckoutId == Guid.Empty || requestedStudents.Length > 3 ||
            requestedStudents.Contains(Guid.Empty) || requestedStudents.Distinct().Count() != requestedStudents.Length ||
            !Enum.IsDefined(request.Method))
        {
            return Error.Validation("Payments.InvalidCheckout", "Provide a checkout ID, payment method and valid students.");
        }

        // Catalog from the design, with 20% off twelve monthly payments.
        int amount = (request.Plan, request.Term) switch
        {
            (PaidPlan.Plus, BillingTerm.Monthly) => 14900,
            (PaidPlan.Family, BillingTerm.Monthly) => 29900,
            (PaidPlan.Plus, BillingTerm.Yearly) => 143040,
            (PaidPlan.Family, BillingTerm.Yearly) => 287040,
            _ => 0
        };
        if (amount == 0)
        {
            return Error.Validation("Payments.InvalidPlan", "Choose Plus or Family and a monthly or yearly term.");
        }

        ApplicationUser? buyer = await db.Users.SingleOrDefaultAsync(
            user => user.Id == buyerId && !user.IsDisabled, cancellationToken);
        if (buyer is null)
        {
            return Error.Unauthorized("Payments.BuyerUnavailable", "The buyer account is unavailable.");
        }

        Guid[] beneficiaries;
        if (isParent)
        {
            int maximumStudents = request.Plan == PaidPlan.Plus ? 1 : 3;
            if (requestedStudents.Length == 0 || requestedStudents.Length > maximumStudents)
            {
                return Error.Validation("Payments.InvalidBeneficiaries",
                    "Choose one linked student for Plus or one to three linked students for Family.");
            }

            int linkedCount = await db.ParentStudentRelationships
                .Where(link => link.Parent.UserId == buyerId && link.Status == RelationshipStatus.Active &&
                               requestedStudents.Contains(link.StudentId) && !link.Student.User.IsDisabled)
                .Select(link => link.StudentId).Distinct().CountAsync(cancellationToken);
            if (linkedCount != requestedStudents.Length)
            {
                return Error.Forbidden("Payments.StudentNotLinked", "Choose students with an active link to your account.");
            }

            beneficiaries = requestedStudents;
        }
        else
        {
            if (request.Plan != PaidPlan.Plus)
            {
                return Error.Forbidden("Payments.ParentPlanRequired", "Family is available to parent accounts.");
            }

            Guid studentId = await db.Students.Where(student => student.UserId == buyerId)
                .Select(student => student.Id).SingleOrDefaultAsync(cancellationToken);
            if (studentId == Guid.Empty || (requestedStudents.Length > 0 && !requestedStudents.SequenceEqual([studentId])))
            {
                return Error.Forbidden("Payments.StudentMustBuyForSelf", "Students can buy Plus for their own account.");
            }

            beneficiaries = [studentId];
        }

        bool isSandbox = !options.Value.EnableLiveCheckout;
        PaymentPurchase? existing = await FindAsync(buyerId, request.CheckoutId, isSandbox, cancellationToken);
        if (existing is not null)
        {
            return Resume(existing);
        }

        PaymobAccountOptions settings = options.Value.ForMode(isSandbox);
        int integration = request.Method == PaymentMethod.Card ? settings.CardIntegrationId : settings.WalletIntegrationId;
        if (!settings.IsConfigured(isSandbox) || integration <= 0)
        {
            return Error.Failure("Payments.MethodUnavailable", "This payment method is unavailable.");
        }

        string phone = (request.BillingPhoneNumber ?? buyer.PhoneNumber ?? "").Trim();
        if (string.IsNullOrWhiteSpace(buyer.Email) || phone.Length is < 10 or > 16 ||
            !phone.TrimStart('+').All(char.IsAsciiDigit) || phone.Count(character => character == '+') > 1)
        {
            return Error.Validation("Payments.InvalidBillingDetails", "Provide a valid billing phone number and account email.");
        }

        PaymentPurchase purchase = new()
        {
            Id = request.CheckoutId,
            BuyerId = buyerId,
            IsSandbox = isSandbox,
            Plan = request.Plan,
            Term = request.Term,
            Method = request.Method,
            StudentIds = beneficiaries,
            AmountPiasters = amount,
            IntegrationId = integration,
            CreatedAtUtc = clock.GetUtcNow()
        };

        db.Set<PaymentPurchase>().Add(purchase);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.Entry(purchase).State = EntityState.Detached;
            existing = await FindAsync(buyerId, request.CheckoutId, isSandbox, cancellationToken);
            if (existing is null)
            {
                throw;
            }

            return Resume(existing);
        }

        try
        {
            (purchase.ProviderOrderId, purchase.CheckoutUrl) =
                await paymob.CreateIntentionAsync(purchase, buyer, phone, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or OperationCanceledException)
        {
            // The session URL was never returned. The same ID must not create a second provider order.
            logger.LogWarning("Paymob checkout {CheckoutId} could not open ({ErrorType}).",
                purchase.Id, exception.GetType().Name);
            purchase.Status = PaymentStatus.Failed;
            await db.SaveChangesAsync(CancellationToken.None);
            return Error.Failure("Payments.CheckoutUnavailable", "Checkout could not be opened. Start a new checkout.");
        }

        // Persist the order mapping before exposing the URL, even if the buyer disconnected.
        await db.SaveChangesAsync(CancellationToken.None);
        return purchase.ToResponse(clock.GetUtcNow());

        Result<CheckoutResponse> Resume(PaymentPurchase previous)
        {
            if (previous.Plan != request.Plan || previous.Term != request.Term || previous.Method != request.Method ||
                !previous.StudentIds.SequenceEqual(beneficiaries))
            {
                return Error.Conflict("Payments.CheckoutIdReused", "Use a new checkout ID for different purchase details.");
            }

            return previous.ToResponse(clock.GetUtcNow());
        }
    }

    public async Task<CheckoutResponse?> GetAsync(Guid buyerId, Guid checkoutId, CancellationToken cancellationToken)
    {
        PaymentPurchase? purchase = await FindAsync(buyerId, checkoutId, !options.Value.EnableLiveCheckout, cancellationToken);
        return purchase?.ToResponse(clock.GetUtcNow());
    }

    public async Task<bool> ProcessCallbackAsync(bool isSandbox, JsonElement callback, string? signature,
        CancellationToken cancellationToken)
    {
        PaymobAccountOptions settings = options.Value.ForMode(isSandbox);
        if (!settings.IsConfigured(isSandbox) || callback.ValueKind != JsonValueKind.Object ||
            !callback.TryGetProperty("obj", out JsonElement transaction) ||
            !PaymobClient.VerifySignature(transaction, signature, settings.HmacSecret))
        {
            return false;
        }

        long orderId;
        long transactionId;
        int amount;
        int integration;
        string? currency;
        bool paid;
        try
        {
            orderId = transaction.GetProperty("order").GetProperty("id").GetInt64();
            transactionId = transaction.GetProperty("id").GetInt64();
            amount = transaction.GetProperty("amount_cents").GetInt32();
            currency = transaction.GetProperty("currency").GetString();
            integration = transaction.GetProperty("integration_id").GetInt32();
            if (transaction.GetProperty("pending").GetBoolean() ||
                transaction.GetProperty("is_refunded").GetBoolean() ||
                transaction.GetProperty("is_voided").GetBoolean() ||
                transaction.GetProperty("has_parent_transaction").GetBoolean() ||
                (transaction.GetProperty("is_auth").GetBoolean() && !transaction.GetProperty("is_capture").GetBoolean()))
            {
                return true;
            }

            paid = transaction.GetProperty("success").GetBoolean() && !transaction.GetProperty("error_occured").GetBoolean();
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException)
        {
            return false;
        }

        PaymentPurchase? purchase = await db.Set<PaymentPurchase>()
            .SingleOrDefaultAsync(item => item.IsSandbox == isSandbox && item.ProviderOrderId == orderId, cancellationToken);
        if (purchase is null)
        {
            return true;
        }

        if (transactionId <= 0 || purchase.AmountPiasters != amount || currency != PaymentPurchase.Currency ||
            purchase.IntegrationId != integration)
        {
            return false;
        }

        while (purchase.Status != PaymentStatus.Paid)
        {
            if (!paid)
            {
                purchase.Status = PaymentStatus.Failed;
            }
            else
            {
                purchase.RecordPayment(transactionId, clock.GetUtcNow());
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateConcurrencyException)
            {
                await db.Entry(purchase).ReloadAsync(cancellationToken);
                if (db.Entry(purchase).State == EntityState.Detached)
                {
                    break;
                }
            }
        }

        return true;
    }

    private Task<PaymentPurchase?> FindAsync(Guid buyerId, Guid checkoutId, bool isSandbox,
        CancellationToken cancellationToken) =>
        db.Set<PaymentPurchase>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.BuyerId == buyerId && item.Id == checkoutId && item.IsSandbox == isSandbox, cancellationToken);
}

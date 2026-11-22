using System.Net.Mail;
using FocusLens.Application.Payments;
using FocusLens.Contracts.Payments;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.StudySessions;
using FocusLens.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FocusLens.Infrastructure.Payments;

internal sealed class BillingService(ApplicationDbContext db, IOptions<PaymobOptions> options, TimeProvider clock) : IBilling
{
    private bool IsSandbox => !options.Value.EnableLiveCheckout;

    private IQueryable<PaymentPurchase> Purchases(Guid buyerId) =>
        db.Set<PaymentPurchase>().Where(purchase => purchase.BuyerId == buyerId && purchase.IsSandbox == IsSandbox);

    private IQueryable<ParentStudentRelationship> Links(Guid buyerId) =>
        db.ParentStudentRelationships.Where(link => link.Parent.UserId == buyerId && !link.Student.User.IsDisabled &&
            (link.Status == RelationshipStatus.Active || link.Status == RelationshipStatus.PausedForBilling));

    private static IOrderedQueryable<PaymentPurchase> LatestPaid(IQueryable<PaymentPurchase> purchases) =>
        purchases.Where(purchase => purchase.Status == PaymentStatus.Paid)
            .OrderByDescending(purchase => purchase.PaidAtUtc).ThenByDescending(purchase => purchase.Id);

    public async Task<Result<BillingOverview>> GetAsync(Guid buyerId, CancellationToken cancellationToken)
    {
        ApplicationUser? buyer = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == buyerId && !user.IsDisabled, cancellationToken);
        if (buyer is null)
        {
            return Error.Unauthorized("Payments.BuyerUnavailable", "The buyer account is unavailable.");
        }

        DateTimeOffset now = clock.GetUtcNow();
        IQueryable<PaymentPurchase> purchases = Purchases(buyerId).AsNoTracking();
        PaymentPurchase? paid = await LatestPaid(purchases).FirstOrDefaultAsync(cancellationToken);
        DateTimeOffset? lastPaid = paid?.PaidAtUtc;
        PaymentPurchase? latestAttempt = await purchases
            .Where(purchase => purchase.Status != PaymentStatus.Paid &&
                (lastPaid == null || purchase.CreatedAtUtc >= lastPaid))
            .OrderByDescending(purchase => purchase.CreatedAtUtc).ThenByDescending(purchase => purchase.Id)
            .FirstOrDefaultAsync(cancellationToken);
        bool hasUsablePending = latestAttempt is { Status: PaymentStatus.Pending } &&
            latestAttempt.CreatedAtUtc + PaymentPurchase.CheckoutLifetime > now;

        bool active = paid?.PaidThroughUtc > now;
        bool cancellation = paid?.CancellationRequestedAtUtc is not null;
        BillingStatus status = (active, cancellation, paid is not null, latestAttempt?.Status) switch
        {
            (true, true, _, _) => BillingStatus.CancellationScheduled,
            (true, false, _, _) => BillingStatus.Active,
            (false, true, _, _) => BillingStatus.Free,
            (false, false, _, PaymentStatus.Failed) => BillingStatus.PaymentFailed,
            (false, false, true, _) => BillingStatus.RenewalDue,
            _ => BillingStatus.Free
        };

        var students = await Links(buyerId).AsNoTracking().Select(link => new
        {
            link.StudentId, link.Status, link.Student.PreferredName, link.Student.User.FirstName,
            link.Student.User.LastName, link.Student.ShareSessionSummariesWithParents
        }).OrderBy(student => student.FirstName).ThenBy(student => student.StudentId).ToArrayAsync(cancellationToken);

        Guid[] beneficiaries = active ? paid!.StudentIds : [];
        DateTimeOffset? usageFrom = null;
        DateTimeOffset? usageTo = null;
        Dictionary<Guid, int> counts = [];
        if (active && paid!.PaidAtUtc is DateTimeOffset start)
        {
            int months = (now.Year - start.Year) * 12 + now.Month - start.Month;
            months = Math.Max(months - (start.AddMonths(months) > now ? 1 : 0), 0);
            usageFrom = start.AddMonths(months);
            usageTo = start.AddMonths(months + 1);
            Guid[] visible = students.Where(student => student.ShareSessionSummariesWithParents &&
                student.Status == RelationshipStatus.Active && beneficiaries.Contains(student.StudentId))
                .Select(student => student.StudentId).ToArray();
            if (visible.Length > 0)
            {
                counts = await db.StudySessions.AsNoTracking().Where(session => visible.Contains(session.StudentId) &&
                    session.Status == StudySessionStatus.Completed && session.CompletedAtUtc >= usageFrom &&
                    session.CompletedAtUtc < usageTo)
                    .GroupBy(session => session.StudentId)
                    .ToDictionaryAsync(group => group.Key, group => group.Count(), cancellationToken);
            }
        }

        BillingChild[] children = students.Select(student => new BillingChild(student.StudentId,
            student.PreferredName ?? $"{student.FirstName} {student.LastName}".Trim(),
            student.Status == RelationshipStatus.PausedForBilling ||
            (IsSandbox && cancellation && !active && student.StudentId != paid!.KeepStudentId),
            active && student.ShareSessionSummariesWithParents && student.Status == RelationshipStatus.Active &&
                beneficiaries.Contains(student.StudentId) ? counts.GetValueOrDefault(student.StudentId) : null)).ToArray();

        BillingContact? contact = await db.Set<BillingContact>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.BuyerId == buyerId, cancellationToken);
        PaymobAccountOptions settings = options.Value.ForMode(IsSandbox);
        bool configured = settings.IsConfigured(IsSandbox);
        return new BillingOverview(status, IsSandbox, paid?.ToBillingPayment(),
            hasUsablePending || latestAttempt?.Status == PaymentStatus.Failed ? latestAttempt?.ToBillingPayment() : null,
            contact?.ToResponse() ?? new($"{buyer.FirstName} {buyer.LastName}".Trim(), buyer.Email ?? "", null, null, null),
            children, beneficiaries, cancellation ? paid!.PaidThroughUtc : null,
            cancellation ? paid!.KeepStudentId : null, usageFrom, usageTo,
            configured && settings.CardIntegrationId > 0, configured && settings.WalletIntegrationId > 0);
    }

    public async Task<Result<BillingOverview>> ScheduleCancellationAsync(Guid buyerId, ScheduleCancellationRequest request,
        CancellationToken cancellationToken)
    {
        PaymentPurchase? purchase = await LatestPaid(Purchases(buyerId)).FirstOrDefaultAsync(cancellationToken);
        if (purchase is null || purchase.Id != request.CheckoutId)
        {
            return Error.NotFound("Payments.PlanNotFound", "The current paid plan was not found.");
        }

        if (purchase.CancellationRequestedAtUtc is not null && purchase.KeepStudentId == request.KeepStudentId)
        {
            return await GetAsync(buyerId, cancellationToken);
        }

        if (purchase.CancellationApplied)
        {
            return Error.Conflict("Payments.CancellationApplied", "The scheduled change has already taken effect.");
        }

        Guid[] active = await Links(buyerId).Where(link => link.Status == RelationshipStatus.Active)
            .Select(link => link.StudentId).ToArrayAsync(cancellationToken);
        if ((active.Length > 0 && request.KeepStudentId is null) ||
            (request.KeepStudentId is Guid studentId && !active.Contains(studentId)))
        {
            return Error.Validation("Payments.InvalidRetainedStudent", "Choose one of your connected students.");
        }

        purchase.CancellationRequestedAtUtc = clock.GetUtcNow();
        purchase.KeepStudentId = request.KeepStudentId;
        return await SaveChangeAsync(buyerId, cancellationToken);
    }

    public async Task<Result<BillingOverview>> ResumeAsync(Guid buyerId, Guid checkoutId, CancellationToken cancellationToken)
    {
        PaymentPurchase? purchase = await LatestPaid(Purchases(buyerId)).FirstOrDefaultAsync(cancellationToken);
        if (purchase is null || purchase.Id != checkoutId)
        {
            return Error.NotFound("Payments.PlanNotFound", "The current paid plan was not found.");
        }

        if (purchase.CancellationApplied || purchase.PaidThroughUtc <= clock.GetUtcNow())
        {
            return Error.Conflict("Payments.PlanEnded", "The paid period has ended. Start a new checkout to renew it.");
        }

        purchase.CancellationRequestedAtUtc = null;
        purchase.KeepStudentId = null;
        return await SaveChangeAsync(buyerId, cancellationToken);
    }

    public async Task<Result<BillingContactResponse>> UpdateContactAsync(Guid buyerId, UpdateBillingContactRequest request,
        CancellationToken cancellationToken)
    {
        string name = request.FullName?.Trim() ?? "";
        string email = request.Email?.Trim() ?? "";
        string? city = TrimOptional(request.City);
        string? country = TrimOptional(request.Country)?.ToUpperInvariant();
        string? address = TrimOptional(request.Address);
        if (name.Length is < 1 or > 150 || email.Length > 254 ||
            !MailAddress.TryCreate(email, out MailAddress? parsed) || parsed.Address != email ||
            city?.Length > 100 || address?.Length > 300 ||
            (country is not null && (country.Length != 2 || !country.All(char.IsAsciiLetter))))
        {
            return Error.Validation("Payments.InvalidBillingContact", "Provide a name, email and a valid two-letter country code.");
        }

        if (!await db.Users.AnyAsync(user => user.Id == buyerId && !user.IsDisabled, cancellationToken))
        {
            return Error.Unauthorized("Payments.BuyerUnavailable", "The buyer account is unavailable.");
        }

        BillingContact? contact = await db.Set<BillingContact>().SingleOrDefaultAsync(item => item.BuyerId == buyerId,
            cancellationToken);
        if (contact is null)
        {
            contact = new BillingContact { BuyerId = buyerId };
            db.Set<BillingContact>().Add(contact);
        }

        contact.FullName = name;
        contact.Email = email;
        contact.City = city;
        contact.Country = country;
        contact.Address = address;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (db.Entry(contact).State == EntityState.Added &&
            db.Set<BillingContact>().AsNoTracking().Any(item => item.BuyerId == buyerId))
        {
            return Error.Conflict("Payments.BillingChanged", "Billing information changed. Reload it and try again.");
        }

        return contact.ToResponse();
    }

    private async Task<Result<BillingOverview>> SaveChangeAsync(Guid buyerId, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Error.Conflict("Payments.BillingChanged", "Billing changed. Reload and try again.");
        }

        return await GetAsync(buyerId, cancellationToken);
    }

    private static string? TrimOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

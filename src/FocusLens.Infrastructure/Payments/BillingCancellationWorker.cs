using FocusLens.Contracts.Payments;
using FocusLens.Domain.Access;
using FocusLens.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FocusLens.Infrastructure.Payments;

internal sealed class BillingCancellationWorker(
    IServiceScopeFactory scopes, TimeProvider clock, ILogger<BillingCancellationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromMinutes(1), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ApplyDueCancellationsAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Could not apply scheduled billing changes.");
            }
        }
    }

    private async Task ApplyDueCancellationsAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopes.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        DateTimeOffset now = clock.GetUtcNow();
        var due = await db.Set<PaymentPurchase>().AsNoTracking()
            .Where(item => item.Status == PaymentStatus.Paid && item.CancellationRequestedAtUtc != null &&
                !item.CancellationApplied && item.PaidThroughUtc <= now)
            .OrderBy(item => item.PaidThroughUtc)
            .Select(item => new { item.BuyerId, item.IsSandbox, item.Id })
            .Take(100).ToArrayAsync(cancellationToken);

        foreach (var key in due)
        {
            PaymentPurchase? purchase = await db.Set<PaymentPurchase>().SingleOrDefaultAsync(item =>
                item.BuyerId == key.BuyerId && item.IsSandbox == key.IsSandbox && item.Id == key.Id &&
                !item.CancellationApplied && item.CancellationRequestedAtUtc != null, cancellationToken);
            if (purchase is null)
            {
                continue;
            }

            // A later confirmed purchase supersedes the old plan. Sandbox records never change real links.
            Guid latest = await db.Set<PaymentPurchase>().Where(item => item.BuyerId == purchase.BuyerId &&
                item.IsSandbox == purchase.IsSandbox && item.Status == PaymentStatus.Paid)
                .OrderByDescending(item => item.PaidAtUtc).ThenByDescending(item => item.Id)
                .Select(item => item.Id).FirstAsync(cancellationToken);
            if (!purchase.IsSandbox && latest == purchase.Id)
            {
                List<ParentStudentRelationship> links = await db.ParentStudentRelationships.Include(link => link.Parent)
                    .Where(link => link.Parent.UserId == purchase.BuyerId && link.Status == RelationshipStatus.Active)
                    .ToListAsync(cancellationToken);
                ParentStudentRelationship? kept = links.SingleOrDefault(link => link.StudentId == purchase.KeepStudentId);
                // Never choose an alternative child if the selected relationship was revoked in the meantime.
                foreach (ParentStudentRelationship link in links.Where(link => link != kept))
                {
                    link.PauseForBilling();
                }

                links.FirstOrDefault()?.Parent.SetActiveChildId(kept?.StudentId);
            }

            purchase.CancellationApplied = true;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // A concurrent resume, cancellation edit, or relationship change will be picked up on a later pass.
            }

            db.ChangeTracker.Clear();
        }
    }
}

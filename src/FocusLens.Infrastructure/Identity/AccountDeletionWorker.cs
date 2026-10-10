using FocusLens.Domain;
using FocusLens.Domain.Identity;
using FocusLens.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FocusLens.Infrastructure.Identity;

/// <summary>Physically removes accounts whose restore window has elapsed.</summary>
public sealed class AccountDeletionWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<AccountDeletionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DeleteExpiredAccountsAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Failed to permanently delete expired accounts.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    internal async Task DeleteExpiredAccountsAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        DateTimeOffset now = timeProvider.GetUtcNow();
        List<Guid> userIds = await db.Users
            .Where(user => user.IsDisabled && user.DeletedAtUtc != null && user.RestoreUntilUtc != null
                           && user.RestoreUntilUtc <= now)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        foreach (Guid userId in userIds)
        {
            await PermanentlyDeleteAsync(db, userId, cancellationToken);
        }
    }

    private static async Task PermanentlyDeleteAsync(
        ApplicationDbContext db,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        Parent? parent = await db.Parents.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        Student? student = await db.Students.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (parent is not null)
        {
            // Activation copies data into the Student profile; removing this setup
            // record never removes that independently-owned profile.
            await db.ChildSetupDrafts
                .Where(draft => draft.ParentId == parent.Id)
                .ExecuteDeleteAsync(cancellationToken);
            await db.StudyGoalProposals.Where(proposal => proposal.ParentId == parent.Id)
                .ExecuteDeleteAsync(cancellationToken);
            await db.ParentStudentRelationships.Where(relationship => relationship.ParentId == parent.Id)
                .ExecuteDeleteAsync(cancellationToken);
            db.Parents.Remove(parent);
        }

        if (student is not null)
        {
            await db.ChildSetupDrafts.Where(draft => draft.ClaimedByStudentId == student.Id)
                .ExecuteDeleteAsync(cancellationToken);
            await db.StudentParentInvitations.Where(invitation => invitation.StudentId == student.Id)
                .ExecuteDeleteAsync(cancellationToken);
            await db.StudyGoalProposals.Where(proposal => proposal.StudentId == student.Id)
                .ExecuteDeleteAsync(cancellationToken);
            await db.ParentStudentRelationships.Where(relationship => relationship.StudentId == student.Id)
                .ExecuteDeleteAsync(cancellationToken);
            db.Students.Remove(student);
        }

        // These are user-owned records. Other dependent profile data is removed by
        // the configured database cascades when the profile/account is removed.
        await db.RefreshTokens.Where(token => token.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await db.Notifications.Where(notification => notification.RecipientUserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await db.UserDeviceTokens.Where(token => token.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await db.UserTermsAcceptances.Where(acceptance => acceptance.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await db.StudentNotificationPreferences.Where(preference => preference.StudentUserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        ApplicationUser? user = await db.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is not null)
        {
            db.Users.Remove(user);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
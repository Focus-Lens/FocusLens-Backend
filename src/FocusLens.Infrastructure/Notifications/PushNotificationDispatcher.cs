using FocusLens.Application.Common.Interfaces;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FocusLens.Infrastructure.Notifications;

public sealed class PushNotificationDispatcher(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<PushNotificationDispatcher> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(30));

        while (!stoppingToken.IsCancellationRequested)
        {
            await DispatchPendingAsync(stoppingToken);

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IPushNotificationProvider provider = scope.ServiceProvider.GetRequiredService<IPushNotificationProvider>();

        if (!provider.IsConfigured)
        {
            return;
        }

        IBaseRepository<Notification> notificationRepository =
            scope.ServiceProvider.GetRequiredService<IBaseRepository<Notification>>();
        IBaseRepository<UserDeviceToken> tokenRepository =
            scope.ServiceProvider.GetRequiredService<IBaseRepository<UserDeviceToken>>();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        List<Notification> notifications = notificationRepository.GetAll()
            .Where(notification =>
                notification.PushSentAtUtc == null &&
                !notification.RecipientUser.IsDisabled &&
                notification.RecipientUser.DeletedAtUtc == null)
            .OrderBy(notification => notification.CreatedAtUtc)
            .Take(25)
            .ToList();

        foreach (Notification notification in notifications)
        {
            List<UserDeviceToken> tokens = tokenRepository.GetAll()
                .Where(token =>
                    token.UserId == notification.RecipientUserId &&
                    token.DisabledAtUtc == null)
                .ToList();

            if (tokens.Count == 0)
            {
                notification.MarkPushAttemptFailed(timeProvider.GetUtcNow(), "No active device tokens.");
                notificationRepository.Update(notification);
                continue;
            }

            bool sent = false;
            string? failureReason = null;

            foreach (UserDeviceToken token in tokens)
            {
                PushNotificationSendResult result =
                    await provider.SendAsync(token, notification, cancellationToken);

                if (result.Succeeded)
                {
                    sent = true;
                    continue;
                }

                failureReason = result.FailureReason;

                if (result.IsUnregistered)
                {
                    token.Disable(timeProvider.GetUtcNow());
                    tokenRepository.Update(token);
                }
            }

            if (sent)
            {
                notification.MarkPushSent(timeProvider.GetUtcNow());
            }
            else
            {
                notification.MarkPushAttemptFailed(
                    timeProvider.GetUtcNow(),
                    failureReason ?? "Push delivery failed.");
            }

            notificationRepository.Update(notification);
        }

        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist push notification dispatch results.");
        }
    }
}

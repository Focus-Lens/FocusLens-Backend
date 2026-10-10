using FocusLens.Application.Common.Interfaces;
using FocusLens.Domain.Notifications;

namespace FocusLens.Infrastructure.Notifications;

public sealed class NoopPushNotificationProvider : IPushNotificationProvider
{
    public bool IsConfigured => false;

    public Task<PushNotificationSendResult> SendAsync(
        UserDeviceToken token,
        Notification notification,
        CancellationToken cancellationToken) =>
        Task.FromResult(PushNotificationSendResult.Failed("Push provider is not configured."));
}
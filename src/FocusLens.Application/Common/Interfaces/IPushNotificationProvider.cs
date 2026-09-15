using FocusLens.Domain.Notifications;

namespace FocusLens.Application.Common.Interfaces;

public interface IPushNotificationProvider
{
    bool IsConfigured { get; }

    Task<PushNotificationSendResult> SendAsync(
        UserDeviceToken token,
        Notification notification,
        CancellationToken cancellationToken);
}

public sealed record PushNotificationSendResult(
    bool Succeeded,
    bool IsUnregistered,
    string? FailureReason)
{
    public static PushNotificationSendResult Success() => new(true, false, null);

    public static PushNotificationSendResult Failed(string reason) => new(false, false, reason);

    public static PushNotificationSendResult Unregistered(string reason) => new(false, true, reason);
}

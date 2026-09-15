using FocusLens.Contracts.Notifications;
using FocusLens.Domain.Notifications;

namespace FocusLens.Application.Notifications;

public static class NotificationMappings
{
    public static NotificationResponse ToResponse(this Notification notification)
    {
        NotificationActionResponse? action = notification.ActionUrl is null
            ? null
            : new NotificationActionResponse(notification.ActionUrl, notification.ActionText);

        return new NotificationResponse(
            notification.Id,
            notification.Title,
            notification.Message,
            notification.Audience.ToString(),
            notification.Category.ToString(),
            notification.CreatedAtUtc,
            notification.IsRead,
            notification.ReadAtUtc,
            action);
    }

    public static StudentNotificationPreferencesResponse ToResponse(
        this StudentNotificationPreferences preferences)
    {
        return new StudentNotificationPreferencesResponse(
            preferences.StudyRemindersEnabled,
            preferences.SessionSummariesEnabled,
            preferences.ParentActivityEnabled,
            preferences.ReminderTime.ToString());
    }

    public static DeviceTokenResponse ToResponse(this UserDeviceToken token)
    {
        return new DeviceTokenResponse(
            token.Id,
            token.Platform.ToString(),
            token.RegisteredAtUtc,
            token.LastSeenAtUtc);
    }
}

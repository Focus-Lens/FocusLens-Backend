namespace FocusLens.Contracts.Notifications;

public sealed record NotificationResponse(
    Guid Id,
    string Title,
    string Message,
    string Type,
    string Category,
    DateTimeOffset CreatedAtUtc,
    bool IsRead,
    DateTimeOffset? ReadAtUtc,
    NotificationActionResponse? Action);

public sealed record NotificationActionResponse(
    string Url,
    string? Text);

public sealed record StudentNotificationPreferencesResponse(
    bool StudyRemindersEnabled,
    bool SessionSummariesEnabled,
    bool ParentActivityEnabled,
    string ReminderTime);

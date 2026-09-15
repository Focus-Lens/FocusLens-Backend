using FocusLens.Domain.Notifications;

namespace FocusLens.Application.Common.Interfaces;

public interface INotificationWriter
{
    Task AddAsync(
        Guid recipientUserId,
        NotificationAudience audience,
        NotificationCategory category,
        string title,
        string message,
        string? actionUrl = null,
        string? actionText = null,
        string? dedupeKey = null);
}

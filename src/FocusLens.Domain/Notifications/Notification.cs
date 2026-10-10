using FocusLens.Domain.Common;
using FocusLens.Domain.Identity;

namespace FocusLens.Domain.Notifications;

public sealed class Notification : AuditableEntity
{
    private Notification()
    {
    }

    public Notification(
        Guid recipientUserId,
        NotificationAudience audience,
        NotificationCategory category,
        string title,
        string message,
        string? actionUrl = null,
        string? actionText = null,
        string? dedupeKey = null)
        : base(Guid.CreateVersion7())
    {
        if (recipientUserId == Guid.Empty)
        {
            throw new ArgumentException("Recipient user ID cannot be empty.", nameof(recipientUserId));
        }

        RecipientUserId = recipientUserId;
        Audience = audience;
        Category = category;
        Title = Normalize(title, nameof(title), 160);
        Message = Normalize(message, nameof(message), 500);
        ActionUrl = NormalizeOptional(actionUrl, 500);
        ActionText = NormalizeOptional(actionText, 80);
        DedupeKey = NormalizeOptional(dedupeKey, 300);
    }

    public Guid RecipientUserId { get; private set; }

    public ApplicationUser RecipientUser { get; private set; } = null!;

    public NotificationAudience Audience { get; private set; }

    public NotificationCategory Category { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public string? ActionUrl { get; private set; }

    public string? ActionText { get; private set; }

    public string? DedupeKey { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    public DateTimeOffset? PushSentAtUtc { get; private set; }

    public DateTimeOffset? PushAttemptedAtUtc { get; private set; }

    public string? PushFailureReason { get; private set; }

    public bool IsRead => ReadAtUtc is not null;

    public void MarkRead(DateTimeOffset readAtUtc) => ReadAtUtc ??= readAtUtc;

    public void MarkPushSent(DateTimeOffset sentAtUtc)
    {
        PushSentAtUtc ??= sentAtUtc;
        PushAttemptedAtUtc = sentAtUtc;
        PushFailureReason = null;
    }

    public void MarkPushAttemptFailed(DateTimeOffset attemptedAtUtc, string reason)
    {
        PushAttemptedAtUtc = attemptedAtUtc;
        PushFailureReason = NormalizeOptional(reason, 300);
    }

    private static string Normalize(string value, string paramName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", paramName);
        }

        string normalized = value.Trim();
        return normalized.Length > maxLength ? normalized[..maxLength] : normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        return normalized.Length > maxLength ? normalized[..maxLength] : normalized;
    }
}
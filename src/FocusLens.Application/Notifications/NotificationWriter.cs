using FocusLens.Application.Common.Interfaces;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Notifications;

namespace FocusLens.Application.Notifications;

public sealed class NotificationWriter(
    IBaseRepository<Notification> notificationRepository,
    IBaseRepository<StudentNotificationPreferences> preferencesRepository)
    : INotificationWriter
{
    public async Task AddAsync(
        Guid recipientUserId,
        NotificationAudience audience,
        NotificationCategory category,
        string title,
        string message,
        string? actionUrl = null,
        string? actionText = null,
        string? dedupeKey = null)
    {
        if (audience == NotificationAudience.Student &&
            category is NotificationCategory.StudyReminder or
                NotificationCategory.SessionSummaryReady or
                NotificationCategory.ParentActivity or
                NotificationCategory.StudyGoalProposal or
                NotificationCategory.ParentConnectionConfirmed)
        {
            StudentNotificationPreferences? preferences =
                await preferencesRepository.FirstOrDefaultAsync(item => item.StudentUserId == recipientUserId);

            if (preferences is not null &&
                category == NotificationCategory.StudyReminder &&
                !preferences.StudyRemindersEnabled)
            {
                return;
            }

            if (preferences is not null &&
                category == NotificationCategory.SessionSummaryReady &&
                !preferences.SessionSummariesEnabled)
            {
                return;
            }

            if (preferences is not null &&
                category is NotificationCategory.ParentActivity or
                    NotificationCategory.StudyGoalProposal or
                    NotificationCategory.ParentConnectionConfirmed &&
                !preferences.ParentActivityEnabled)
            {
                return;
            }
        }

        if (dedupeKey is not null)
        {
            Notification? existing =
                await notificationRepository.FirstOrDefaultAsync(notification => notification.DedupeKey == dedupeKey);
            if (existing is not null)
            {
                return;
            }
        }

        notificationRepository.Add(new Notification(
            recipientUserId,
            audience,
            category,
            title,
            message,
            actionUrl,
            actionText,
            dedupeKey));
    }
}
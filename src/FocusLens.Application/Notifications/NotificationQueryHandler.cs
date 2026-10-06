using FocusLens.Application.Common.Interfaces;
using FocusLens.Contracts.Notifications;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Notifications;
using MediatR;

namespace FocusLens.Application.Notifications;

public sealed class NotificationQueryHandler(
    IBaseRepository<Notification> notificationRepository,
    IBaseRepository<StudentNotificationPreferences> preferencesRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyNotificationsQuery, NotificationsResponse>,
        IRequestHandler<GetMyStudentNotificationPreferencesQuery, StudentNotificationPreferencesResponse>
{
    public async Task<NotificationsResponse> Handle(
        GetMyNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return new NotificationsResponse([], 0);
        }

        IEnumerable<Notification> notifications = request.Filter switch
        {
            NotificationFilter.Alerts => await notificationRepository.GetAllAsync(notification =>
                notification.RecipientUserId == userId && AlertCategories.Contains(notification.Category)),
            NotificationFilter.System => await notificationRepository.GetAllAsync(notification =>
                notification.RecipientUserId == userId && SystemCategories.Contains(notification.Category)),
            _ => await notificationRepository.GetAllAsync(notification =>
                notification.RecipientUserId == userId)
        };

        int unreadCount = await notificationRepository.CountAsync(notification =>
            notification.RecipientUserId == userId && notification.ReadAtUtc == null);

        return new NotificationsResponse(
            notifications
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .Select(notification => notification.ToResponse())
            .ToList(),
            unreadCount);
    }

    public async Task<StudentNotificationPreferencesResponse> Handle(
        GetMyStudentNotificationPreferencesQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return DefaultStudentPreferences();
        }

        StudentNotificationPreferences? preferences = await preferencesRepository.FirstOrDefaultAsync(
            preferences => preferences.StudentUserId == userId);

        return (preferences ?? new StudentNotificationPreferences(userId)).ToResponse();
    }

    private static StudentNotificationPreferencesResponse DefaultStudentPreferences() =>
        new(true, true, true, StudentReminderTime.EightAm.ToString());

    private static readonly NotificationCategory[] AlertCategories =
    [
        NotificationCategory.FocusPatternChanged,
        NotificationCategory.SessionSummaryReady,
        NotificationCategory.SubjectProgressReportReady,
        NotificationCategory.ParentConnectionConfirmed,
        NotificationCategory.StudyReminder,
        NotificationCategory.StudyTip,
        NotificationCategory.WeeklyProgressUpdate,
        NotificationCategory.StudyGoalProposal,
        NotificationCategory.ParentActivity
    ];

    private static readonly NotificationCategory[] SystemCategories =
    [
        NotificationCategory.PrivacyInformationUpdated,
        NotificationCategory.System
    ];
}

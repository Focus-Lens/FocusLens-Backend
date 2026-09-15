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
    : IRequestHandler<GetMyNotificationsQuery, IReadOnlyList<NotificationResponse>>,
        IRequestHandler<GetMyStudentNotificationPreferencesQuery, StudentNotificationPreferencesResponse>
{
    public async Task<IReadOnlyList<NotificationResponse>> Handle(
        GetMyNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return [];
        }

        IEnumerable<Notification> query = await notificationRepository.GetAllAsync(
            notification =>
                notification.RecipientUserId == userId &&
                !notification.RecipientUser.IsDisabled &&
                notification.RecipientUser.DeletedAtUtc == null,
            notification => notification.RecipientUser);

        query = request.Filter switch
        {
            NotificationFilter.Unread => query.Where(notification => notification.ReadAtUtc == null),
            NotificationFilter.System => query.Where(notification =>
                notification.Category == NotificationCategory.System),
            _ => query
        };

        return query
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .Select(notification => notification.ToResponse())
            .ToList();
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
}

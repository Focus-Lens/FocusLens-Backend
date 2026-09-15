using FocusLens.Contracts.Notifications;
using MediatR;

namespace FocusLens.Application.Notifications;

public sealed record GetMyNotificationsQuery(NotificationFilter Filter)
    : IRequest<IReadOnlyList<NotificationResponse>>;

public sealed record GetMyStudentNotificationPreferencesQuery()
    : IRequest<StudentNotificationPreferencesResponse>;

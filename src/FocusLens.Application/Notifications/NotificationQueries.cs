using FocusLens.Contracts.Notifications;
using MediatR;

namespace FocusLens.Application.Notifications;

public sealed record GetMyNotificationsQuery(NotificationFilter Filter)
    : IRequest<NotificationsResponse>;

public sealed record GetMyStudentNotificationPreferencesQuery : IRequest<StudentNotificationPreferencesResponse>;
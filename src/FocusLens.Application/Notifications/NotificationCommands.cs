using FocusLens.Contracts.Notifications;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Notifications;

public sealed record MarkNotificationReadCommand(Guid NotificationId)
    : IRequest<Result<NotificationResponse>>;

public sealed record MarkAllNotificationsReadCommand()
    : IRequest<Result<Success>>;

public sealed record UpdateStudentNotificationPreferencesCommand(
    UpdateStudentNotificationPreferencesRequest Request)
    : IRequest<Result<StudentNotificationPreferencesResponse>>;

public sealed record RegisterDeviceTokenCommand(RegisterDeviceTokenRequest Request)
    : IRequest<Result<DeviceTokenResponse>>;

public sealed record RemoveDeviceTokenCommand(Guid DeviceTokenId)
    : IRequest<Result<Success>>;

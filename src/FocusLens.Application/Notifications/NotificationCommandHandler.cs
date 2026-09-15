using FocusLens.Application.Common.Interfaces;
using FocusLens.Contracts.Notifications;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Notifications;
using MediatR;

namespace FocusLens.Application.Notifications;

public sealed class NotificationCommandHandler(
    IBaseRepository<Notification> notificationRepository,
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudentNotificationPreferences> preferencesRepository,
    IBaseRepository<UserDeviceToken> deviceTokenRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<MarkNotificationReadCommand, Result<NotificationResponse>>,
        IRequestHandler<UpdateStudentNotificationPreferencesCommand, Result<StudentNotificationPreferencesResponse>>,
        IRequestHandler<RegisterDeviceTokenCommand, Result<DeviceTokenResponse>>,
        IRequestHandler<RemoveDeviceTokenCommand, Result<Success>>
{
    public async Task<Result<NotificationResponse>> Handle(
        MarkNotificationReadCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Notifications.CurrentUserUnavailable",
                "The current user could not be identified.");
        }

        Notification? notification = await notificationRepository.FirstOrDefaultAsync(
            notification => notification.Id == request.NotificationId &&
                            notification.RecipientUserId == userId &&
                            !notification.RecipientUser.IsDisabled &&
                            notification.RecipientUser.DeletedAtUtc == null,
            notification => notification.RecipientUser);

        if (notification is null)
        {
            return Error.NotFound(
                "Notifications.NotFound",
                "The notification was not found.");
        }

        notification.MarkRead(timeProvider.GetUtcNow());
        notificationRepository.Update(notification);
        await unitOfWork.SaveChangesAsync();

        return notification.ToResponse();
    }

    public async Task<Result<StudentNotificationPreferencesResponse>> Handle(
        UpdateStudentNotificationPreferencesCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Notifications.CurrentUserUnavailable",
                "The current user could not be identified.");
        }

        if (!request.Request.StudyRemindersEnabledProvided &&
            !request.Request.SessionSummariesEnabledProvided &&
            !request.Request.ParentActivityEnabledProvided &&
            !request.Request.ReminderTimeProvided)
        {
            return Error.Validation(
                "Notifications.NoPreferencesProvided",
                "Provide at least one notification preference to update.");
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(
            student => student.UserId == userId,
            student => student.User);

        if (student is null || student.User.IsDisabled || student.User.DeletedAtUtc is not null)
        {
            return Error.NotFound(
                "Notifications.StudentNotFound",
                "The student profile was not found.");
        }

        if (!TryParseReminderTime(request.Request.ReminderTime, out StudentReminderTime? reminderTime))
        {
            return Error.Validation(
                "Notifications.InvalidReminderTime",
                "Reminder time must be 8 AM, 10 AM, or 6 PM.");
        }

        StudentNotificationPreferences? preferences =
            await preferencesRepository.FirstOrDefaultAsync(preferences => preferences.StudentUserId == userId);

        if (preferences is null)
        {
            preferences = new StudentNotificationPreferences(userId);
            preferencesRepository.Add(preferences);
        }

        preferences.Update(
            request.Request.StudyRemindersEnabledProvided
                ? request.Request.StudyRemindersEnabled
                : null,
            request.Request.SessionSummariesEnabledProvided
                ? request.Request.SessionSummariesEnabled
                : null,
            request.Request.ParentActivityEnabledProvided
                ? request.Request.ParentActivityEnabled
                : null,
            request.Request.ReminderTimeProvided
                ? reminderTime
                : null);

        await unitOfWork.SaveChangesAsync();

        return preferences.ToResponse();
    }

    public async Task<Result<DeviceTokenResponse>> Handle(
        RegisterDeviceTokenCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Notifications.CurrentUserUnavailable",
                "The current user could not be identified.");
        }

        if (!TryParsePlatform(request.Request.Platform, out DeviceTokenPlatform platform))
        {
            return Error.Validation(
                "Notifications.InvalidDevicePlatform",
                "Device platform must be Android, Ios, or Web.");
        }

        if (string.IsNullOrWhiteSpace(request.Request.Token))
        {
            return Error.Validation(
                "Notifications.DeviceTokenRequired",
                "Device token is required.");
        }

        string normalizedToken = request.Request.Token.Trim();
        DateTimeOffset now = timeProvider.GetUtcNow();

        UserDeviceToken? existing =
            await deviceTokenRepository.FirstOrDefaultAsync(token => token.Token == normalizedToken);

        if (existing is not null && existing.UserId != userId)
        {
            return Error.Conflict(
                "Notifications.DeviceTokenRegisteredToAnotherUser",
                "This device token is already registered to another account.");
        }

        if (existing is null)
        {
            existing = new UserDeviceToken(userId, platform, normalizedToken, now);
            deviceTokenRepository.Add(existing);
        }
        else
        {
            existing.Touch(platform, now);
            deviceTokenRepository.Update(existing);
        }

        await unitOfWork.SaveChangesAsync();

        return existing.ToResponse();
    }

    public async Task<Result<Success>> Handle(
        RemoveDeviceTokenCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Notifications.CurrentUserUnavailable",
                "The current user could not be identified.");
        }

        UserDeviceToken? token = await deviceTokenRepository.FirstOrDefaultAsync(
            token => token.Id == request.DeviceTokenId && token.UserId == userId);

        if (token is null)
        {
            return Error.NotFound(
                "Notifications.DeviceTokenNotFound",
                "The device token was not found.");
        }

        token.Disable(timeProvider.GetUtcNow());
        deviceTokenRepository.Update(token);
        await unitOfWork.SaveChangesAsync();

        return Result.Success;
    }

    private static bool TryParseReminderTime(string? value, out StudentReminderTime? reminderTime)
    {
        reminderTime = null;

        if (value is null)
        {
            return true;
        }

        reminderTime = value.Trim().ToUpperInvariant() switch
        {
            "8AM" or "8 AM" or "EIGHTAM" or "EIGHT_AM" => StudentReminderTime.EightAm,
            "10AM" or "10 AM" or "TENAM" or "TEN_AM" => StudentReminderTime.TenAm,
            "6PM" or "6 PM" or "SIXPM" or "SIX_PM" => StudentReminderTime.SixPm,
            _ => null
        };

        return reminderTime is not null;
    }

    private static bool TryParsePlatform(string? value, out DeviceTokenPlatform platform) =>
        Enum.TryParse(value?.Trim(), ignoreCase: true, out platform) &&
        Enum.IsDefined(platform);
}

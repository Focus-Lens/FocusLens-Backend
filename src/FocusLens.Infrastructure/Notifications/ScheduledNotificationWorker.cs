using FocusLens.Application.Common.Interfaces;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Notifications;
using FocusLens.Domain.StudySessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FocusLens.Infrastructure.Notifications;

public sealed class ScheduledNotificationWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<ScheduledNotificationWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromMinutes(5));

        while (!stoppingToken.IsCancellationRequested)
        {
            await CreateDueNotificationsAsync(stoppingToken);

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CreateDueNotificationsAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IBaseRepository<Student> studentRepository = scope.ServiceProvider.GetRequiredService<IBaseRepository<Student>>();
        IBaseRepository<StudentNotificationPreferences> preferencesRepository =
            scope.ServiceProvider.GetRequiredService<IBaseRepository<StudentNotificationPreferences>>();
        IBaseRepository<ParentStudentRelationship> relationshipRepository =
            scope.ServiceProvider.GetRequiredService<IBaseRepository<ParentStudentRelationship>>();
        IBaseRepository<StudySession> sessionRepository =
            scope.ServiceProvider.GetRequiredService<IBaseRepository<StudySession>>();
        INotificationWriter notificationWriter = scope.ServiceProvider.GetRequiredService<INotificationWriter>();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        DateTimeOffset now = timeProvider.GetUtcNow();
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        int currentHour = now.UtcDateTime.Hour;

        try
        {
            List<StudentNotificationPreferences> preferences =
                (await preferencesRepository.GetAllAsync(
                    item =>
                        item.StudyRemindersEnabled &&
                        (int)item.ReminderTime <= currentHour &&
                        !item.StudentUser.IsDisabled &&
                        item.StudentUser.DeletedAtUtc == null,
                    item => item.StudentUser))
                .ToList();

            foreach (StudentNotificationPreferences preference in preferences)
            {
                await notificationWriter.AddAsync(
                    preference.StudentUserId,
                    NotificationAudience.Student,
                    NotificationCategory.StudyReminder,
                    "Study reminder",
                    "It is time for your planned study session.",
                    "/students/me/study-sessions",
                    "Start studying",
                    $"study-reminder:{preference.StudentUserId}:{today:yyyyMMdd}:{preference.ReminderTime}");
            }

            await CreateWeeklyParentProgressAsync(
                studentRepository,
                relationshipRepository,
                sessionRepository,
                notificationWriter,
                now);

            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to create scheduled notifications.");
        }
    }

    private static async Task CreateWeeklyParentProgressAsync(
        IBaseRepository<Student> studentRepository,
        IBaseRepository<ParentStudentRelationship> relationshipRepository,
        IBaseRepository<StudySession> sessionRepository,
        INotificationWriter notificationWriter,
        DateTimeOffset now)
    {
        if (now.UtcDateTime.DayOfWeek != DayOfWeek.Monday || now.UtcDateTime.Hour < 9)
        {
            return;
        }

        DateOnly weekStart = DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(-7));
        DateTimeOffset rangeStart = new(now.UtcDateTime.Date.AddDays(-7), TimeSpan.Zero);
        DateTimeOffset rangeEnd = new(now.UtcDateTime.Date, TimeSpan.Zero);

        List<Student> students = (await studentRepository.GetAllAsync(
                student =>
                    student.ShareSubjectTrendsWithParents &&
                    !student.User.IsDisabled &&
                    student.User.DeletedAtUtc == null,
                student => student.User))
            .ToList();

        foreach (Student student in students)
        {
            bool hasCompletedStudy = sessionRepository.GetAll()
                .Any(session =>
                    session.StudentId == student.Id &&
                    session.Status == StudySessionStatus.Completed &&
                    session.CompletedAtUtc >= rangeStart &&
                    session.CompletedAtUtc < rangeEnd);

            if (!hasCompletedStudy)
            {
                continue;
            }

            List<ParentStudentRelationship> relationships =
                (await relationshipRepository.GetAllAsync(
                    relationship =>
                    relationship.StudentId == student.Id &&
                    relationship.Status == RelationshipStatus.Active &&
                    !relationship.Parent.User.IsDisabled &&
                    relationship.Parent.User.DeletedAtUtc == null,
                    relationship => relationship.Parent,
                    relationship => relationship.Parent.User))
                .ToList();

            foreach (ParentStudentRelationship relationship in relationships)
            {
                await notificationWriter.AddAsync(
                    relationship.Parent.UserId,
                    NotificationAudience.Parent,
                    NotificationCategory.WeeklyProgressUpdate,
                    "Weekly progress update",
                    "A connected student's weekly study progress is ready.",
                    $"/parents/students/{student.Id}/dashboard",
                    "View progress",
                    $"weekly-progress:{student.Id}:{relationship.Parent.UserId}:{weekStart:yyyyMMdd}");
            }
        }
    }
}

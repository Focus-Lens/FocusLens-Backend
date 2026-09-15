using FocusLens.Application.Common.Interfaces;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Notifications;

namespace FocusLens.Application.Notifications;

public sealed class StudyNotificationService(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    INotificationWriter notificationWriter)
{
    public async Task NotifySessionCompletedAsync(Guid sessionId, Guid studentId)
    {
        Student? student = await studentRepository.FirstOrDefaultAsync(
            item => item.Id == studentId,
            item => item.User);

        if (student is null || student.User.IsDisabled || student.User.DeletedAtUtc is not null)
        {
            return;
        }

        await notificationWriter.AddAsync(
            student.UserId,
            NotificationAudience.Student,
            NotificationCategory.SessionSummaryReady,
            "Session summary ready",
            "Your study session summary is ready to review.",
            $"/students/me/reports/sessions/{sessionId}",
            "View summary",
            $"session:{sessionId}:summary:student");

        IEnumerable<ParentStudentRelationship> relationships =
            await relationshipRepository.GetAllAsync(
                relationship => relationship.StudentId == studentId &&
                                relationship.Status == RelationshipStatus.Active,
                relationship => relationship.Parent,
                relationship => relationship.Parent.User);

        foreach (ParentStudentRelationship relationship in relationships)
        {
            if (relationship.Parent.User.IsDisabled ||
                relationship.Parent.User.DeletedAtUtc is not null)
            {
                continue;
            }

            if (student.ShareSessionSummariesWithParents)
            {
                await notificationWriter.AddAsync(
                    relationship.Parent.UserId,
                    NotificationAudience.Parent,
                    NotificationCategory.SessionSummaryReady,
                    "Session summary ready",
                    "A connected student's study session summary is ready.",
                    $"/parents/students/{studentId}/dashboard/sessions",
                    "View summary",
                    $"session:{sessionId}:summary:parent:{relationship.Parent.UserId}");
            }

            if (student.ShareSubjectTrendsWithParents)
            {
                await notificationWriter.AddAsync(
                    relationship.Parent.UserId,
                    NotificationAudience.Parent,
                    NotificationCategory.SubjectProgressReportReady,
                    "Subject progress report ready",
                    "New subject progress is available for a connected student.",
                    $"/parents/students/{studentId}/dashboard",
                    "View progress",
                    $"session:{sessionId}:subject-progress:{relationship.Parent.UserId}");
            }
        }
    }
}

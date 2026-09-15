using FocusLens.Domain.Common;
using FocusLens.Domain.Identity;

namespace FocusLens.Domain.Notifications;

public sealed class StudentNotificationPreferences : AuditableEntity
{
    private StudentNotificationPreferences()
    {
    }

    public StudentNotificationPreferences(Guid studentUserId)
        : base(Guid.CreateVersion7())
    {
        if (studentUserId == Guid.Empty)
        {
            throw new ArgumentException("Student user ID cannot be empty.", nameof(studentUserId));
        }

        StudentUserId = studentUserId;
        StudyRemindersEnabled = true;
        SessionSummariesEnabled = true;
        ParentActivityEnabled = true;
        ReminderTime = StudentReminderTime.EightAm;
    }

    public Guid StudentUserId { get; private set; }

    public ApplicationUser StudentUser { get; private set; } = null!;

    public bool StudyRemindersEnabled { get; private set; }

    public bool SessionSummariesEnabled { get; private set; }

    public bool ParentActivityEnabled { get; private set; }

    public StudentReminderTime ReminderTime { get; private set; }

    public void Update(
        bool? studyRemindersEnabled,
        bool? sessionSummariesEnabled,
        bool? parentActivityEnabled,
        StudentReminderTime? reminderTime)
    {
        if (studyRemindersEnabled is not null)
        {
            StudyRemindersEnabled = studyRemindersEnabled.Value;
        }

        if (sessionSummariesEnabled is not null)
        {
            SessionSummariesEnabled = sessionSummariesEnabled.Value;
        }

        if (parentActivityEnabled is not null)
        {
            ParentActivityEnabled = parentActivityEnabled.Value;
        }

        if (reminderTime is not null)
        {
            ReminderTime = reminderTime.Value;
        }
    }
}

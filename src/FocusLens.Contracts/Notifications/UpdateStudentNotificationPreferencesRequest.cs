using System.Text.Json.Serialization;

namespace FocusLens.Contracts.Notifications;

public sealed class UpdateStudentNotificationPreferencesRequest
{
    private bool? _parentActivityEnabled;
    private string? _reminderTime;
    private bool? _sessionSummariesEnabled;
    private bool? _studyRemindersEnabled;

    public bool? StudyRemindersEnabled
    {
        get => _studyRemindersEnabled;
        set
        {
            StudyRemindersEnabledProvided = true;
            _studyRemindersEnabled = value;
        }
    }

    public bool? SessionSummariesEnabled
    {
        get => _sessionSummariesEnabled;
        set
        {
            SessionSummariesEnabledProvided = true;
            _sessionSummariesEnabled = value;
        }
    }

    public bool? ParentActivityEnabled
    {
        get => _parentActivityEnabled;
        set
        {
            ParentActivityEnabledProvided = true;
            _parentActivityEnabled = value;
        }
    }

    public string? ReminderTime
    {
        get => _reminderTime;
        set
        {
            ReminderTimeProvided = true;
            _reminderTime = value;
        }
    }

    [JsonIgnore] public bool StudyRemindersEnabledProvided { get; private set; }

    [JsonIgnore] public bool SessionSummariesEnabledProvided { get; private set; }

    [JsonIgnore] public bool ParentActivityEnabledProvided { get; private set; }

    [JsonIgnore] public bool ReminderTimeProvided { get; private set; }
}
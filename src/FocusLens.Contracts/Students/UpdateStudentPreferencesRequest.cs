using System.Text.Json.Serialization;

namespace FocusLens.Contracts.Students;

public sealed class UpdateStudentPreferencesRequest
{
    private string? _customGrade;
    private DateOnly? _dateOfBirth;
    private StudentGoal? _goal;
    private StudentGrade? _grade;
    private string? _preferredName;
    private bool? _shareDetailedAnswersWithParents;
    private bool? _shareSessionSummariesWithParents;
    private bool? _shareSubjectTrendsWithParents;

    private StudyTimeGoalRequest? _studyTimeGoal;
    private IReadOnlyCollection<StudentSubjectRequest>? _subjects;
    private DayOfWeek? _weekStartsOn;

    public DateOnly? DateOfBirth
    {
        get => _dateOfBirth;
        set
        {
            DateOfBirthProvided = true;
            _dateOfBirth = value;
        }
    }

    public StudentGoal? Goal
    {
        get => _goal;
        set
        {
            GoalProvided = true;
            _goal = value;
        }
    }

    public StudyTimeGoalRequest? StudyTimeGoal
    {
        get => _studyTimeGoal;
        set
        {
            StudyTimeGoalProvided = true;
            _studyTimeGoal = value;
        }
    }

    public StudentGrade? Grade
    {
        get => _grade;
        set
        {
            GradeProvided = true;
            _grade = value;
        }
    }

    public string? CustomGrade
    {
        get => _customGrade;
        set
        {
            CustomGradeProvided = true;
            _customGrade = value;
        }
    }

    public string? PreferredName
    {
        get => _preferredName;
        set
        {
            PreferredNameProvided = true;
            _preferredName = value;
        }
    }

    public IReadOnlyCollection<StudentSubjectRequest>? Subjects
    {
        get => _subjects;
        set
        {
            SubjectsProvided = true;
            _subjects = value;
        }
    }

    public bool? ShareSessionSummariesWithParents
    {
        get => _shareSessionSummariesWithParents;
        set
        {
            ShareSessionSummariesWithParentsProvided = true;
            _shareSessionSummariesWithParents = value;
        }
    }

    public bool? ShareSubjectTrendsWithParents
    {
        get => _shareSubjectTrendsWithParents;
        set
        {
            ShareSubjectTrendsWithParentsProvided = true;
            _shareSubjectTrendsWithParents = value;
        }
    }

    public bool? ShareDetailedAnswersWithParents
    {
        get => _shareDetailedAnswersWithParents;
        set
        {
            ShareDetailedAnswersWithParentsProvided = true;
            _shareDetailedAnswersWithParents = value;
        }
    }

    public DayOfWeek? WeekStartsOn
    {
        get => _weekStartsOn;
        set
        {
            WeekStartsOnProvided = true;
            _weekStartsOn = value;
        }
    }

    [JsonIgnore] public bool DateOfBirthProvided { get; private set; }

    [JsonIgnore] public bool GoalProvided { get; private set; }


    [JsonIgnore] public bool StudyTimeGoalProvided { get; private set; }

    [JsonIgnore] public bool GradeProvided { get; private set; }

    [JsonIgnore] public bool CustomGradeProvided { get; private set; }

    [JsonIgnore] public bool PreferredNameProvided { get; private set; }

    [JsonIgnore] public bool SubjectsProvided { get; private set; }

    [JsonIgnore] public bool ShareSessionSummariesWithParentsProvided { get; private set; }

    [JsonIgnore] public bool ShareSubjectTrendsWithParentsProvided { get; private set; }

    [JsonIgnore] public bool ShareDetailedAnswersWithParentsProvided { get; private set; }

    [JsonIgnore] public bool WeekStartsOnProvided { get; private set; }
}
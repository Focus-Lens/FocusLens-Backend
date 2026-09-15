using System.Text.Json.Serialization;

namespace FocusLens.Contracts.Students;

public sealed class UpdateStudentPreferencesRequest
{
    private DateOnly? _dateOfBirth;
    private IReadOnlyCollection<StudentGoal>? _goals;
    private StudentGrade? _grade;
    private string? _preferredName;
    private bool? _shareSessionSummariesWithParents;
    private bool? _shareSubjectTrendsWithParents;
    private IReadOnlyCollection<StudyPriority>? _studyPriorities;
    private StudyTimeGoalRequest? _studyTimeGoal;
    private IReadOnlyCollection<StudentSubjectRequest>? _subjects;

    public DateOnly? DateOfBirth
    {
        get => _dateOfBirth;
        set
        {
            DateOfBirthProvided = true;
            _dateOfBirth = value;
        }
    }

    public IReadOnlyCollection<StudentGoal>? Goals
    {
        get => _goals;
        set
        {
            GoalsProvided = true;
            _goals = value;
        }
    }

    public IReadOnlyCollection<StudyPriority>? StudyPriorities
    {
        get => _studyPriorities;
        set
        {
            StudyPrioritiesProvided = true;
            _studyPriorities = value;
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

    [JsonIgnore] public bool DateOfBirthProvided { get; private set; }

    [JsonIgnore] public bool GoalsProvided { get; private set; }

    [JsonIgnore] public bool StudyPrioritiesProvided { get; private set; }

    [JsonIgnore] public bool StudyTimeGoalProvided { get; private set; }

    [JsonIgnore] public bool GradeProvided { get; private set; }

    [JsonIgnore] public bool PreferredNameProvided { get; private set; }

    [JsonIgnore] public bool SubjectsProvided { get; private set; }

    [JsonIgnore] public bool ShareSessionSummariesWithParentsProvided { get; private set; }

    [JsonIgnore] public bool ShareSubjectTrendsWithParentsProvided { get; private set; }
}

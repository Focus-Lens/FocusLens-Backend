using System.Text.Json.Serialization;

namespace FocusLens.Contracts.Students;

public sealed class UpdateStudentPreferencesRequest
{
    private StudentGoal? _goal;
    private StudentGrade? _grade;
    private IReadOnlyCollection<StudentSubjectRequest>? _subjects;

    // Setters record property presence so PATCH can distinguish omitted/null/value.
    public StudentGoal? Goal
    {
        get => _goal;
        set { GoalProvided = true; _goal = value; }
    }

    public StudentGrade? Grade
    {
        get => _grade;
        set { GradeProvided = true; _grade = value; }
    }

    public IReadOnlyCollection<StudentSubjectRequest>? Subjects
    {
        get => _subjects;
        set { SubjectsProvided = true; _subjects = value; }
    }

    [JsonIgnore]
    public bool GoalProvided { get; private set; }

    [JsonIgnore]
    public bool GradeProvided { get; private set; }

    [JsonIgnore]
    public bool SubjectsProvided { get; private set; }
}

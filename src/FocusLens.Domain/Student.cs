using FocusLens.Domain.Common;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Students;

namespace FocusLens.Domain;

public class Student : AuditableEntity
{
    private Student() { }

    public Student(Guid userId)
        : base(Guid.CreateVersion7())
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        }

        UserId = userId;
    }

    public Guid UserId { get; private set; }

    public ApplicationUser User { get; private set; } = null!;

    public string? PreferredName { get; private set; }

    public DateOnly? DateOfBirth { get; private set; }

    public ICollection<StudentGoal> Goals { get; } = [];

    public StudentGrade? Grade { get; private set; }

    public ICollection<StudentSubject> Subjects { get; } = [];

    public ICollection<StudyPriority> StudyPriorities { get; } = [];

    public StudyTimeGoal? StudyTimeGoal { get; private set; }

    public bool IsOnboardingCompleted { get; private set; }

    public void SetDateOfBirth(DateOnly? dateOfBirth) => DateOfBirth = dateOfBirth;

    public void ReplaceGoals(IEnumerable<StudentGoal> goals)
    {
        ArgumentNullException.ThrowIfNull(goals);

        StudentGoal[] values = goals.ToArray();

        if (values.Distinct().Count() != values.Length)
        {
            throw new ArgumentException("Student goals cannot contain duplicates.", nameof(goals));
        }

        Goals.Clear();

        foreach (StudentGoal goal in values)
        {
            Goals.Add(goal);
        }

        UpdateOnboardingCompletionStatus();
    }

    public void SetGrade(StudentGrade? grade)
    {
        Grade = grade;
        UpdateOnboardingCompletionStatus();
    }

    public void ReplaceSubjects(IEnumerable<StudentSubject> subjects)
    {
        ArgumentNullException.ThrowIfNull(subjects);

        Subjects.Clear();

        foreach (StudentSubject subject in subjects)
        {
            Subjects.Add(subject);
        }

        UpdateOnboardingCompletionStatus();
    }

    public void ReplaceStudyPriorities(IEnumerable<StudyPriority> priorities)
    {
        ArgumentNullException.ThrowIfNull(priorities);

        StudyPriority[] values = priorities.ToArray();

        if (values.Distinct().Count() != values.Length)
        {
            throw new ArgumentException(
                "Study priorities cannot contain duplicates.",
                nameof(priorities)
            );
        }

        StudyPriorities.Clear();

        foreach (StudyPriority priority in values)
        {
            StudyPriorities.Add(priority);
        }
    }

    public void SetStudyTimeGoal(StudyTimeGoal? studyTimeGoal) => StudyTimeGoal = studyTimeGoal;

    public void SetPreferredName(string? preferredName) => PreferredName = preferredName?.Trim();

    public void CompleteOnboarding(
        IEnumerable<StudentGoal> goals,
        StudentGrade? grade,
        IEnumerable<StudentSubject> subjects
    )
    {
        ArgumentNullException.ThrowIfNull(goals);
        ArgumentNullException.ThrowIfNull(subjects);

        ReplaceGoals(goals);

        Grade = grade;
        ReplaceSubjects(subjects);
    }

    private void UpdateOnboardingCompletionStatus() =>
        IsOnboardingCompleted = Goals.Count > 0 && Grade is not null && Subjects.Count > 0;
}
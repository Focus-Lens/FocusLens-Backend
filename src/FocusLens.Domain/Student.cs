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

    public StudentGoal? Goal { get; private set; }

    public StudentGrade? Grade { get; private set; }

    public ICollection<StudentSubject> Subjects { get; private set; }
        = new List<StudentSubject>();

    public bool IsOnboardingCompleted { get; private set; }

    public void SetGoal(StudentGoal? goal)
    {
        Goal = goal;
        UpdateOnboardingCompletionStatus();
    }

    public void SetGrade(StudentGrade? grade)
    {
        Grade = grade;
        UpdateOnboardingCompletionStatus();
    }

    public void SetPreferredName(string? preferredName)
        => PreferredName = preferredName?.Trim();

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

    public void CompleteOnboarding(
        StudentGoal? goal,
        StudentGrade? grade,
        IEnumerable<StudentSubject> subjects)
    {
        ArgumentNullException.ThrowIfNull(subjects);

        List<StudentSubject> subjectList = subjects.ToList();

        Goal = goal;
        Grade = grade;
        ReplaceSubjects(subjectList);
    }

    private void UpdateOnboardingCompletionStatus()
        => IsOnboardingCompleted = Goal is not null
            && Grade is not null
            && Subjects.Count > 0;
}

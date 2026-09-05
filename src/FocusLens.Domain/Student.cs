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

    public StudentGoal? Goal { get; private set; }

    public StudentGrade? Grade { get; private set; }

    public ICollection<StudentSubject> Subjects { get; private set; }
        = new List<StudentSubject>();

    public bool IsOnboardingCompleted { get; private set; }

    public void SetGoal(StudentGoal? goal) => Goal = goal;

    public void SetGrade(StudentGrade? grade) => Grade = grade;

    public void ReplaceSubjects(IEnumerable<StudentSubject> subjects)
    {
        ArgumentNullException.ThrowIfNull(subjects);

        Subjects.Clear();

        foreach (StudentSubject subject in subjects)
        {
            Subjects.Add(subject);
        }
    }

    public void CompleteOnboarding(
        StudentGoal goal,
        StudentGrade grade,
        IEnumerable<StudentSubject> subjects)
    {
        ArgumentNullException.ThrowIfNull(subjects);

        List<StudentSubject> subjectList = subjects.ToList();

        if (subjectList.Count == 0)
        {
            throw new ArgumentException(
                "At least one subject is required.",
                nameof(subjects));
        }

        Goal = goal;
        Grade = grade;

        ReplaceSubjects(subjectList);

        IsOnboardingCompleted = true;
    }
}

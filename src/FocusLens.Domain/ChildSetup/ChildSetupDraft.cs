using FocusLens.Domain.Common;
using FocusLens.Domain.Students;

namespace FocusLens.Domain.ChildSetup;

public sealed class ChildSetupDraft : AuditableEntity
{
    private ChildSetupDraft()
    {
    }

    public ChildSetupDraft(Guid parentId)
        : base(Guid.CreateVersion7())
    {
        if (parentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Parent ID cannot be empty.",
                nameof(parentId));
        }

        ParentId = parentId;
        Status = ChildSetupStatus.Draft;
    }

    public Guid ParentId { get; private set; }
    public Guid? ClaimedByStudentId { get; private set; }

    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public StudentGrade? Grade { get; private set; }

    public ICollection<ChildSetupSubject> Subjects { get; }
        = new List<ChildSetupSubject>();

    public ICollection<StudyPriority> StudyPriorities { get; }
        = new List<StudyPriority>();

    public StudyTimeGoal? StudyTimeGoal { get; private set; }
    public string? ProfileImageStorageReference { get; private set; }
    public ChildSetupStatus Status { get; private set; }

    public void SetName(string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new ArgumentException(
                "First name is required.",
                nameof(firstName));
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new ArgumentException(
                "Last name is required.",
                nameof(lastName));
        }

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
    }

    public void SetDateOfBirth(DateOnly? dateOfBirth) => DateOfBirth = dateOfBirth;

    public void SetGrade(StudentGrade? grade) => Grade = grade;

    public void ReplaceSubjects(IEnumerable<ChildSetupSubject> subjects)
    {
        ArgumentNullException.ThrowIfNull(subjects);

        Subjects.Clear();

        foreach (ChildSetupSubject subject in subjects)
        {
            Subjects.Add(subject);
        }
    }

    public void ReplaceStudyPriorities(IEnumerable<StudyPriority> priorities)
    {
        ArgumentNullException.ThrowIfNull(priorities);

        StudyPriority[] values = priorities.ToArray();

        if (values.Distinct().Count() != values.Length)
        {
            throw new ArgumentException(
                "Study priorities cannot contain duplicates.",
                nameof(priorities));
        }

        StudyPriorities.Clear();

        foreach (StudyPriority priority in values)
        {
            StudyPriorities.Add(priority);
        }
    }

    public void SetStudyTimeGoal(StudyTimeGoal? studyTimeGoal) => StudyTimeGoal = studyTimeGoal;

    public void SetProfileImageStorageReference(string? storageReference) =>
        ProfileImageStorageReference = string.IsNullOrWhiteSpace(storageReference)
            ? null
            : storageReference.Trim();

    public void MarkInvited()
    {
        if (Status != ChildSetupStatus.Draft)
        {
            throw new InvalidOperationException(
                "Only a draft can be marked as invited.");
        }

        Status = ChildSetupStatus.Invited;
    }

       public void ResetToDraft()
    {
        if (Status != ChildSetupStatus.Invited)
        {
            throw new InvalidOperationException(
                "Only an invited setup can be reset to draft.");
        }

        Status = ChildSetupStatus.Draft;
    }

    public void MarkInvitationCancelled() => ResetToDraft();

    public void MarkClaimed(Guid studentId)
    {
        if (studentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Student ID cannot be empty.",
                nameof(studentId));
        }

        if (Status != ChildSetupStatus.Invited)
        {
            throw new InvalidOperationException(
                "Only an invited setup can be claimed.");
        }

        ClaimedByStudentId = studentId;
        Status = ChildSetupStatus.Claimed;
    }

    public void MarkActivated()
    {
        if (Status != ChildSetupStatus.Claimed)
        {
            throw new InvalidOperationException(
                "Only a claimed setup can be activated.");
        }

        Status = ChildSetupStatus.Activated;
    }
}

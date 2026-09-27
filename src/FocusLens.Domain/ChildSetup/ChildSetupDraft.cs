using FocusLens.Domain.Common;
using FocusLens.Domain.Students;

namespace FocusLens.Domain.ChildSetup;

public sealed class ChildSetupDraft : AuditableEntity
{
    private ChildSetupDraft() { }

    public ChildSetupDraft(Guid parentId)
        : base(Guid.CreateVersion7())
    {
        if (parentId == Guid.Empty)
        {
            throw new ArgumentException("Parent ID cannot be empty.", nameof(parentId));
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
    public string? CustomGrade { get; private set; }

    public ICollection<ChildSetupSubject> Subjects { get; } = new List<ChildSetupSubject>();

    public StudentGoal? Goal { get; private set; }

    public StudyTimeGoal? StudyTimeGoal { get; private set; }
    public ChildSetupProfileMode? ProfileSetupMode { get; private set; }
    public string? ProfileImageStorageReference { get; private set; }
    public ChildSetupStatus Status { get; private set; }

    public void SetName(string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new ArgumentException("First name is required.", nameof(firstName));
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new ArgumentException("Last name is required.", nameof(lastName));
        }

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
    }

    public void SetDateOfBirth(DateOnly? dateOfBirth) => DateOfBirth = dateOfBirth;

    public void SetGrade(StudentGrade? grade)
    {
        Grade = grade;

        if (grade != StudentGrade.Other)
        {
            CustomGrade = null;
        }
    }

    public void SetCustomGrade(string? customGrade) =>
        CustomGrade = string.IsNullOrWhiteSpace(customGrade) ? null : customGrade.Trim();

    public void ReplaceSubjects(IEnumerable<ChildSetupSubject> subjects)
    {
        ArgumentNullException.ThrowIfNull(subjects);

        Subjects.Clear();

        foreach (ChildSetupSubject subject in subjects)
        {
            Subjects.Add(subject);
        }
    }

    public void SetGoal(StudentGoal? goal) =>
        Goal = goal;

    public void SetStudyTimeGoal(StudyTimeGoal? studyTimeGoal) => StudyTimeGoal = studyTimeGoal;

    public void SetProfileSetupMode(ChildSetupProfileMode mode)
    {
        ProfileSetupMode = mode;
    }

    public bool IsProfileComplete()
    {
        return !string.IsNullOrWhiteSpace(FirstName)
            && !string.IsNullOrWhiteSpace(LastName)
            && Grade is not null
            && (Grade != StudentGrade.Other || !string.IsNullOrWhiteSpace(CustomGrade))
            && Subjects.Count > 0
            && StudyTimeGoal is not null;
    }

    public void SetProfileImageStorageReference(string? storageReference) =>
        ProfileImageStorageReference = string.IsNullOrWhiteSpace(storageReference)
            ? null
            : storageReference.Trim();

    public void MarkInvited()
    {
        if (Status != ChildSetupStatus.Draft)
        {
            throw new InvalidOperationException("Only a draft can be marked as invited.");
        }

        Status = ChildSetupStatus.Invited;
    }

    public void ResetToDraft()
    {
        if (Status != ChildSetupStatus.Invited)
        {
            throw new InvalidOperationException("Only an invited setup can be reset to draft.");
        }

        Status = ChildSetupStatus.Draft;
    }

    public void MarkInvitationCancelled() => ResetToDraft();

    public void MarkClaimed(Guid studentId)
    {
        if (studentId == Guid.Empty)
        {
            throw new ArgumentException("Student ID cannot be empty.", nameof(studentId));
        }

        if (Status != ChildSetupStatus.Invited)
        {
            throw new InvalidOperationException("Only an invited setup can be claimed.");
        }

        ClaimedByStudentId = studentId;
        Status = ChildSetupStatus.Claimed;
    }

    public void MarkActivated()
    {
        if (Status != ChildSetupStatus.Claimed)
        {
            throw new InvalidOperationException("Only a claimed setup can be activated.");
        }

        Status = ChildSetupStatus.Activated;
    }
}

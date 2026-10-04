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

    public string? ProfileImageStorageReference { get; private set; }

    public DateOnly? DateOfBirth { get; private set; }

    public StudentGoal? Goal { get; private set; }

    public StudentGrade? Grade { get; private set; }

    public string? CustomGrade { get; private set; }

    public ICollection<StudentSubject> Subjects { get; } = [];

    public ICollection<StudentWeek> Weeks { get; } = [];


    public StudyTimeGoal? StudyTimeGoal { get; private set; }

    public DayOfWeek? WeekStartsOn { get; private set; }

    // IANA identifier supplied by the student's device. Null means legacy accounts
    // use the explicit UTC fallback until their device reports a time zone.
    public string? TimeZoneId { get; private set; }

    public bool ShareSessionSummariesWithParents { get; private set; }

    public bool ShareSubjectTrendsWithParents { get; private set; }

    public bool ShareDetailedAnswersWithParents { get; private set; }

    public bool IsOnboardingCompleted { get; private set; }

    public void SetDateOfBirth(DateOnly? dateOfBirth)
    {
        DateOfBirth = dateOfBirth;
        UpdateOnboardingCompletionStatus();
    }

    public void SetGoal(StudentGoal? goal)
    {
        Goal = goal;

        UpdateOnboardingCompletionStatus();
    }

    public void SetGrade(StudentGrade? grade)
    {
        Grade = grade;

        if (grade is not StudentGrade.Other)
        {
            CustomGrade = null;
        }

        UpdateOnboardingCompletionStatus();
    }

    public void SetCustomGrade(string? customGrade)
    {
        CustomGrade = customGrade?.Trim();
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

    public void SetStudyTimeGoal(StudyTimeGoal? studyTimeGoal)
    {
        StudyTimeGoal = studyTimeGoal;
        UpdateOnboardingCompletionStatus();
    }

    public void SetWeekStartsOn(DayOfWeek? weekStartsOn)
    {
        if (weekStartsOn.HasValue && !Enum.IsDefined(weekStartsOn.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(weekStartsOn), weekStartsOn, "Week start day is invalid.");
        }

        WeekStartsOn = weekStartsOn;
    }

    public void SetTimeZoneId(string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        TimeZoneId = timeZoneId.Trim();
    }

    public void SetPreferredName(string? preferredName)
    {
        PreferredName = preferredName?.Trim();
        UpdateOnboardingCompletionStatus();
    }

    public void SetProfileImageStorageReference(string? storageReference) =>
        ProfileImageStorageReference = string.IsNullOrWhiteSpace(storageReference)
            ? null
            : storageReference.Trim();

    public void SetParentSharingPreferences(
        bool shareSessionSummaries,
        bool shareSubjectTrends,
        bool shareDetailedAnswers = false)
    {
        ShareSessionSummariesWithParents = shareSessionSummaries;
        ShareSubjectTrendsWithParents = shareSubjectTrends;
        ShareDetailedAnswersWithParents = shareDetailedAnswers;
    }

    public void CompleteOnboarding(
        StudentGoal? goal,
        StudentGrade? grade,
        IEnumerable<StudentSubject> subjects
    )
    {
        ArgumentNullException.ThrowIfNull(subjects);

        SetGoal(goal);

        Grade = grade;
        ReplaceSubjects(subjects);
    }

    public IReadOnlyCollection<string> GetMissingOnboardingFields()
    {
        List<string> missingFields = [];

        if (string.IsNullOrWhiteSpace(PreferredName)) missingFields.Add("preferredName");
        if (DateOfBirth is null) missingFields.Add("dateOfBirth");
        if (Goal is null) missingFields.Add("goal");
        if (Grade is null) missingFields.Add("grade");
        if (Grade is StudentGrade.Other && string.IsNullOrWhiteSpace(CustomGrade)) missingFields.Add("customGrade");
        if (Subjects.Count == 0) missingFields.Add("subjects");
        if (StudyTimeGoal is null) missingFields.Add("studyTimeGoal");

        return missingFields;
    }

    private void UpdateOnboardingCompletionStatus() =>
        IsOnboardingCompleted = GetMissingOnboardingFields().Count == 0;
}

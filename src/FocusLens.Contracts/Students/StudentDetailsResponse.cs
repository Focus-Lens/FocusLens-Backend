namespace FocusLens.Contracts.Students;

public sealed record StudentDetailsResponse(
    Guid Id,
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string? PreferredName,
    string? ProfileImageStorageReference,
    DateOnly? DateOfBirth,
    IReadOnlyCollection<StudentGoal> Goals,
    StudentGrade? Grade,
    string? CustomGrade,
    IReadOnlyCollection<StudentSubjectResponse> Subjects,
    IReadOnlyCollection<StudyPriority> StudyPriorities,
    StudyTimeGoalResponse? StudyTimeGoal,
    bool ShareSessionSummariesWithParents,
    bool ShareSubjectTrendsWithParents,
    bool ShareDetailedAnswersWithParents,
    bool OnboardingCompleted
);

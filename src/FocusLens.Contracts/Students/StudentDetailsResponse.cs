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
    StudentGoal? Goal,
    StudentGrade? Grade,
    string? CustomGrade,
    IReadOnlyCollection<StudentSubjectResponse> Subjects,
        StudyTimeGoalResponse? StudyTimeGoal,
    bool ShareSessionSummariesWithParents,
    bool ShareSubjectTrendsWithParents,
    bool ShareDetailedAnswersWithParents,
    string OnboardingStatus,
    IReadOnlyCollection<string> MissingFields
);

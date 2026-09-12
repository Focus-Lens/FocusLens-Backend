namespace FocusLens.Contracts.Students;

public sealed record StudentDetailsResponse(
    Guid Id,
    Guid UserId,
    string? PreferredName,
    DateOnly? DateOfBirth,
    IReadOnlyCollection<StudentGoal> Goals,
    StudentGrade? Grade,
    IReadOnlyCollection<StudentSubjectResponse> Subjects,
    IReadOnlyCollection<StudyPriority> StudyPriorities,
    StudyTimeGoalResponse? StudyTimeGoal,
    bool OnboardingCompleted
);
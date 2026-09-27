namespace FocusLens.Contracts.Students;

public sealed record CompleteStudentOnboardingRequest(
    string PreferredName,
    DateOnly? DateOfBirth,
    StudentGoal? Goal,
    StudentGrade? Grade,
    IReadOnlyCollection<StudentSubjectRequest>? Subjects,
        StudyTimeGoalRequest? StudyTimeGoal,
    string? CustomGrade = null
);
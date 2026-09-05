namespace FocusLens.Contracts.Students;

public sealed record StudentDetailsResponse(
    Guid Id,
    Guid UserId,
    StudentGoal? Goal,
    StudentGrade? Grade,
    IReadOnlyCollection<StudentSubjectResponse> Subjects,
    bool OnboardingCompleted
);

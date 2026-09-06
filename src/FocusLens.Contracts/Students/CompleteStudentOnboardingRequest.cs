namespace FocusLens.Contracts.Students;

public sealed record CompleteStudentOnboardingRequest(
    StudentGoal? Goal,
    StudentGrade? Grade,
    IReadOnlyCollection<StudentSubjectRequest>? Subjects
);

namespace FocusLens.Contracts.Students;

public sealed record CompleteStudentOnboardingRequest(
    DateOnly? DateOfBirth,
    IReadOnlyCollection<StudentGoal>? Goals,
    StudentGrade? Grade,
    IReadOnlyCollection<StudentSubjectRequest>? Subjects,
    IReadOnlyCollection<StudyPriority>? StudyPriorities,
    StudyTimeGoalRequest? StudyTimeGoal
);
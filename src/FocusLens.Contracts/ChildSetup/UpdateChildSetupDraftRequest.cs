using FocusLens.Contracts.Students;

namespace FocusLens.Contracts.ChildSetup;

public sealed record UpdateChildSetupDraftRequest(
    string FirstName,
    string LastName,
    DateOnly? DateOfBirth,
    StudentGrade? Grade,
    string? CustomGrade,
    IReadOnlyCollection<StudentSubjectRequest>? Subjects,
    StudentGoal? Goal,
    ChildSetupStudyTimeGoalRequest? StudyTimeGoal
);
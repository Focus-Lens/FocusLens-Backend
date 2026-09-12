using FocusLens.Contracts.Students;

namespace FocusLens.Contracts.ChildSetup;

public sealed record UpdateChildSetupDraftRequest(
    string FirstName,
    string LastName,
    DateOnly? DateOfBirth,
    StudentGrade? Grade,
    IReadOnlyCollection<StudentSubjectRequest>? Subjects,
    IReadOnlyCollection<StudyPriority>? StudyPriorities,
    StudyTimeGoalRequest? StudyTimeGoal
);
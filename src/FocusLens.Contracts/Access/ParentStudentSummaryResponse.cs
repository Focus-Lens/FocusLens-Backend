using FocusLens.Contracts.Students;

namespace FocusLens.Contracts.Access;

public sealed record ParentStudentSummaryResponse(
    Guid Id,
    string? PreferredName,
    string FirstName,
    string LastName,
    StudentGrade? Grade,
    IReadOnlyCollection<StudentSubjectResponse> Subjects,
    bool OnboardingCompleted
);
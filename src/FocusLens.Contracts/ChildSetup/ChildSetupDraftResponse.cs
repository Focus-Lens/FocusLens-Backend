using FocusLens.Contracts.Students;

namespace FocusLens.Contracts.ChildSetup;

public sealed record ChildSetupDraftResponse(
    Guid Id,
    string Status,
    string? FirstName,
    string? LastName,
    DateOnly? DateOfBirth,
    StudentGrade? Grade,
    IReadOnlyCollection<ChildSetupSubjectResponse> Subjects,
    IReadOnlyCollection<StudyPriority> StudyPriorities,
    StudyTimeGoalResponse? StudyTimeGoal,
    string? ProfileSetupMode,
    string? ProfileImageStorageReference
);

public sealed record ChildSetupSubjectResponse(
    Guid Id,
    string Type,
    string? CustomName
);
using FocusLens.Contracts.Students;

namespace FocusLens.Contracts.ChildSetup;

public sealed record ChildSetupDraftResponse(
    Guid Id,
    string Status,
    string? FirstName,
    string? LastName,
    DateOnly? DateOfBirth,
    StudentGrade? Grade,
    string? CustomGrade,
    IReadOnlyCollection<ChildSetupSubjectResponse> Subjects,
    StudentGoal? Goal,
    StudyTimeGoalResponse? StudyTimeGoal,
    string? ProfileSetupMode,
    string? ProfileImageStorageReference
);

public sealed record ChildSetupSubjectResponse(
    Guid Id,
    string Type,
    string? CustomName
);
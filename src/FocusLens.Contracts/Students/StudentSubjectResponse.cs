namespace FocusLens.Contracts.Students;

public sealed record StudentSubjectResponse(
    StudentSubjectType Type,
    string? CustomName
);

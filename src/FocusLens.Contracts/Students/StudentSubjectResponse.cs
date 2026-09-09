namespace FocusLens.Contracts.Students;

public sealed record StudentSubjectResponse(
    Guid Id,
    StudentSubjectType Type,
    string? CustomName
);

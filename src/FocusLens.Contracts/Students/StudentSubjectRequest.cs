namespace FocusLens.Contracts.Students;

public sealed record StudentSubjectRequest(
    StudentSubjectType Type,
    string? CustomName
);
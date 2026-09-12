namespace FocusLens.Contracts.Access;

public sealed record StudentParentSummaryResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email
);
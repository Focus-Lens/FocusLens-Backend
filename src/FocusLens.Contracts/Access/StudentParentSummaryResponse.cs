namespace FocusLens.Contracts.Access;

public sealed record StudentParentSummaryResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    Guid? RelationshipId = null,
    string? ConnectionStatus = null
);
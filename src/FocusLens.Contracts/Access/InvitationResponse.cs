namespace FocusLens.Contracts.Access;

public sealed record InvitationResponse(
    Guid Id,
    Guid ParentId,
    Guid StudentId,
    string Status,
    string InitiatedBy,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc,
    string? ParentEmail
);

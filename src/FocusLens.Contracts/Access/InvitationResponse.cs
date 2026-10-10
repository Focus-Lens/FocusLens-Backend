namespace FocusLens.Contracts.Access;

public sealed record InvitationResponse(
    Guid Id,
    Guid? ParentId,
    Guid StudentId,
    string Status,
    string InitiatedBy,
    string Direction,
    string Kind,
    string? OtherPartyEmail,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc,
    string? InvitationUrl = null
);
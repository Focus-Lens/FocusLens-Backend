namespace FocusLens.Contracts.Access;

public sealed record InvitationResponse(
    Guid Id,
    Guid ParentId,
    Guid StudentId,
    string Status,
    DateTimeOffset? RevokedAtUtc,
    string? ParentEmail
);

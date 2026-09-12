namespace FocusLens.Contracts.Access;

public sealed record StudentParentInvitationResponse(
    Guid Id,
    string Status,
    DateTimeOffset ExpiresAtUtc,
    string InvitationUrl);
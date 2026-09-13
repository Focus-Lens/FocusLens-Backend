namespace FocusLens.Contracts.ChildSetup;

public sealed record ChildSetupInvitationResponse(
    Guid Id,
    string Status,
    DateTimeOffset ExpiresAtUtc,
    string InvitationUrl
);

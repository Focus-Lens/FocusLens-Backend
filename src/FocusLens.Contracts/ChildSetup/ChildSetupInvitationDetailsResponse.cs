namespace FocusLens.Contracts.ChildSetup;

public sealed record ChildSetupInvitationDetailsResponse(
    string Status,
    string Type,
    string? TargetEmail,
    DateTimeOffset ExpiresAtUtc
);
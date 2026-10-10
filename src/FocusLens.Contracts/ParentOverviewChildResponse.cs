namespace FocusLens.Contracts;

public sealed record ParentOverviewChildResponse(
    Guid Id,
    string Type,
    string Status,
    string? FirstName,
    string? LastName,
    Guid? StudentId,
    Guid? ChildSetupDraftId,
    Guid? ChildSetupInvitationId,
    string? ChildSetupInvitationStatus,
    DateTimeOffset? ChildSetupInvitationExpiresAtUtc
);
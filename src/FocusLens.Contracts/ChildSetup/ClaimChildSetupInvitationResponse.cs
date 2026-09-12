namespace FocusLens.Contracts.ChildSetup;

public sealed record ClaimChildSetupInvitationResponse(
    Guid DraftId,
    string DraftStatus,
    string InvitationStatus
);
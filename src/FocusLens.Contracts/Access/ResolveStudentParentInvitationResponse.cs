namespace FocusLens.Contracts.Access;

/// <summary>
///     The privacy-safe preview displayed after an invitation link is opened.
/// </summary>
public sealed record ResolveStudentParentInvitationResponse(
    Guid InvitationId,
    string Status,
    string Type,
    string? TargetEmail,
    string StudentPreferredName,
    string? AgeRange,
    DateTimeOffset ExpiresAtUtc,
    bool RequiresSignIn);

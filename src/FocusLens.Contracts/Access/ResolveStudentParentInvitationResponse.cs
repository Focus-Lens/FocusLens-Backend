namespace FocusLens.Contracts.Access;

/// <summary>
///     The privacy-safe preview displayed after an invitation link is opened.
/// </summary>
public sealed record ResolveStudentParentInvitationResponse(
    string Status,
    string StudentPreferredName,
    DateTimeOffset ExpiresAtUtc,
    bool RequiresSignIn);
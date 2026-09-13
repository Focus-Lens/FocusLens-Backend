using FocusLens.Contracts.Students;

namespace FocusLens.Contracts.ChildSetup;

public sealed record ParentChildSetupInvitationResponse(
    Guid DraftId,
    Guid InvitationId,
    string? FirstName,
    string? LastName,
    string TargetEmail,
    string Status,
    DateTimeOffset ExpiresAtUtc,
    StudentGrade? Grade
);

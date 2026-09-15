namespace FocusLens.Application.Features.Users.Dtos;

public sealed record UserAccountStatusDto(
    bool EmailConfirmed,
    string EmailVerificationStatus,
    bool IsDisabled,
    string AccountStatus,
    IReadOnlyCollection<UserChildRelationshipStatusDto> ChildRelationships);

public sealed record UserChildRelationshipStatusDto(
    Guid RelationshipId,
    Guid ChildId,
    string ChildName,
    string RelationshipStatus);

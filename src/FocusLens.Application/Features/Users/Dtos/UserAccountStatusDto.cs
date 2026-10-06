namespace FocusLens.Application.Features.Users.Dtos;

public sealed record UserAccountStatusDto(
    bool EmailConfirmed,
    string EmailVerificationStatus,
    bool IsDisabled,
    string AccountStatus);

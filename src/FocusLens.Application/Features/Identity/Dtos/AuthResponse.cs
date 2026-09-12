namespace FocusLens.Application.Features.Identity.Dtos;

public sealed record AuthResponse(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyCollection<string> Roles,
    TokenResponse? Tokens,
    bool RequiresOnboarding = false,
    string? RegistrationToken = null,
    DateTimeOffset? RegistrationTokenExpiresOnUtc = null,
    bool AccountCreated = false,
    string? OnboardingStatus = null);
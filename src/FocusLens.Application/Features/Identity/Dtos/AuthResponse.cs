namespace FocusLens.Application.Features.Identity.Dtos;

public sealed record AuthResponse(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyCollection<string> Roles,
    TokenResponse? Tokens,
    bool AccountCreated = false,
    string? OnboardingStatus = null);
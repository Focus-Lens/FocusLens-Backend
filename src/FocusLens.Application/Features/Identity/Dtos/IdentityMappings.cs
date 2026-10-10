using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Identity;

namespace FocusLens.Application.Features.Identity.Dtos;

internal static class IdentityMappings
{
    public static TokenResponse ToResponse(this TokenPair tokenPair)
    {
        return new TokenResponse(
            tokenPair.AccessToken,
            tokenPair.AccessTokenExpiresOnUtc,
            tokenPair.RefreshToken,
            tokenPair.RefreshTokenExpiresOnUtc);
    }

    public static AuthResponse ToAuthResponse(
        this ApplicationUser user,
        IReadOnlyCollection<string> roles,
        TokenPair tokenPair,
        Student? student = null,
        bool accountCreated = false)
    {
        return new AuthResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            roles,
            tokenPair.ToResponse(),
            accountCreated,
            student is null
                ? null
                : student.IsOnboardingCompleted
                    ? "completed"
                    : "incomplete");
    }
}
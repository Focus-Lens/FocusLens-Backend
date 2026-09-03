using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;

namespace FocusLens.Application.Features.Identity.Dtos;

internal static class IdentityMappings
{
    public static TokenResponse ToResponse(this TokenPair tokenPair)
        => new(
            tokenPair.AccessToken,
            tokenPair.AccessTokenExpiresOnUtc,
            tokenPair.RefreshToken,
            tokenPair.RefreshTokenExpiresOnUtc);

    public static AuthResponse ToAuthResponse(
        this ApplicationUser user,
        IReadOnlyCollection<string> roles,
        TokenPair tokenPair)
        => new(
            user.Id,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            roles,
            tokenPair.ToResponse());
}

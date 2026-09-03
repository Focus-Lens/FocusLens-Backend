using System.Security.Claims;

using FocusLens.Domain.Identity;

namespace FocusLens.Domain.Interfaces;

public interface ITokenProvider
{
    Task<TokenPair> CreateTokenPairAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default);

    ClaimsPrincipal GetPrincipalFromExpiredToken(string accessToken);

    string GenerateRefreshToken();

    Task<RefreshToken> PersistRefreshTokenAsync(
        Guid userId,
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task<TokenPair?> RotateRefreshTokenAsync(
        ApplicationUser user,
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);
}

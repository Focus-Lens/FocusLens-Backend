using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using FocusLens.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FocusLens.Infrastructure.Authentication;

public sealed class TokenProvider : ITokenProvider
{
    private static readonly TimeSpan ClockSkew = TimeSpan.Zero;

    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtOptions _jwtOptions;
    private readonly TimeProvider _timeProvider;

    public TokenProvider(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IOptions<JwtOptions> jwtOptions,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _jwtOptions = jwtOptions.Value;
        _timeProvider = timeProvider;
    }

    public async Task<TokenPair> CreateTokenPairAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        string accessToken = await GenerateAccessTokenAsync(user);
        DateTimeOffset accessTokenExpiresOnUtc = _timeProvider
            .GetUtcNow()
            .AddMinutes(_jwtOptions.TokenExpirationInMinutes);

        string refreshTokenValue = GenerateRefreshToken();
        RefreshToken refreshToken = await PersistRefreshTokenAsync(
            user.Id,
            refreshTokenValue,
            cancellationToken);

        return new TokenPair(
            accessToken,
            accessTokenExpiresOnUtc,
            refreshTokenValue,
            refreshToken.ExpiresOnUtc);
    }

    public ClaimsPrincipal GetPrincipalFromExpiredToken(string accessToken)
    {
        TokenValidationParameters validationParameters = CreateValidationParameters();
        validationParameters.ValidateLifetime = false;

        JwtSecurityTokenHandler tokenHandler = new();
        ClaimsPrincipal principal = tokenHandler.ValidateToken(
            accessToken,
            validationParameters,
            out SecurityToken securityToken);

        if (securityToken is not JwtSecurityToken jwtSecurityToken
            || !jwtSecurityToken.Header.Alg.Equals(
                SecurityAlgorithms.HmacSha256,
                StringComparison.Ordinal))
        {
            throw new SecurityTokenException("Invalid token.");
        }

        return principal;
    }

    public string GenerateRefreshToken()
    {
        Span<byte> bytes = stackalloc byte[64];
        RandomNumberGenerator.Fill(bytes);

        return Convert.ToBase64String(bytes);
    }

    public async Task<RefreshToken> PersistRefreshTokenAsync(
        Guid userId,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset expiresOnUtc = _timeProvider
            .GetUtcNow()
            .AddDays(_jwtOptions.RefreshTokenExpirationInDays);

        Result<RefreshToken> result = RefreshToken.Create(
            Guid.CreateVersion7(),
            HashRefreshToken(refreshToken),
            userId,
            expiresOnUtc);

        if (result.IsError)
        {
            throw new InvalidOperationException(result.TopError.Description);
        }

        _dbContext.RefreshTokens.Add(result.Value);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return result.Value;
    }

    public async Task<RefreshToken?> GetRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        string hashedToken = HashRefreshToken(refreshToken);

        return await _dbContext.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(token => token.Token == hashedToken, cancellationToken);
    }

    public async Task<TokenPair?> RotateRefreshTokenAsync(
        ApplicationUser user,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        string hashedToken = HashRefreshToken(refreshToken);

        RefreshToken? persistedToken = await _dbContext.RefreshTokens
            .SingleOrDefaultAsync(token => token.Token == hashedToken, cancellationToken);

        if (persistedToken is null
            || persistedToken.UserId != user.Id
            || !persistedToken.IsActive)
        {
            return null;
        }

        Result<Success> revokeResult = persistedToken.Revoke(_timeProvider.GetUtcNow());

        if (revokeResult.IsError)
        {
            return null;
        }

        string accessToken = await GenerateAccessTokenAsync(user);
        DateTimeOffset accessTokenExpiresOnUtc = _timeProvider
            .GetUtcNow()
            .AddMinutes(_jwtOptions.TokenExpirationInMinutes);

        string newRefreshTokenValue = GenerateRefreshToken();
        DateTimeOffset refreshTokenExpiresOnUtc = _timeProvider
            .GetUtcNow()
            .AddDays(_jwtOptions.RefreshTokenExpirationInDays);

        Result<RefreshToken> newRefreshTokenResult = RefreshToken.Create(
            Guid.CreateVersion7(),
            HashRefreshToken(newRefreshTokenValue),
            user.Id,
            refreshTokenExpiresOnUtc);

        if (newRefreshTokenResult.IsError)
        {
            throw new InvalidOperationException(newRefreshTokenResult.TopError.Description);
        }

        _dbContext.RefreshTokens.Add(newRefreshTokenResult.Value);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TokenPair(
            accessToken,
            accessTokenExpiresOnUtc,
            newRefreshTokenValue,
            newRefreshTokenResult.Value.ExpiresOnUtc);
    }

    public async Task<bool> RevokeRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        string hashedToken = HashRefreshToken(refreshToken);

        RefreshToken? persistedToken = await _dbContext.RefreshTokens
            .SingleOrDefaultAsync(token => token.Token == hashedToken, cancellationToken);

        if (persistedToken is null || !persistedToken.IsActive)
        {
            return false;
        }

        Result<Success> result = persistedToken.Revoke(_timeProvider.GetUtcNow());

        if (result.IsError)
        {
            return false;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<string> GenerateAccessTokenAsync(ApplicationUser user)
    {
        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString())
        ];

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        foreach (string role in await _userManager.GetRolesAsync(user))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_jwtOptions.Secret));
        SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);

        JwtSecurityToken token = new(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: utcNow.UtcDateTime,
            expires: utcNow.AddMinutes(_jwtOptions.TokenExpirationInMinutes).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private TokenValidationParameters CreateValidationParameters()
        => new()
        {
            ValidateIssuer = true,
            ValidIssuer = _jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_jwtOptions.Secret)),
            ValidateLifetime = true,
            ClockSkew = ClockSkew
        };

    private static string HashRefreshToken(string refreshToken)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));

        return Convert.ToBase64String(bytes);
    }
}

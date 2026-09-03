namespace FocusLens.Domain.Interfaces;

public sealed record TokenPair(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresOnUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresOnUtc);

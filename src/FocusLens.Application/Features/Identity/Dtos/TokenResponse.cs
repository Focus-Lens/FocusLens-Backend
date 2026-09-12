namespace FocusLens.Application.Features.Identity.Dtos;

public sealed record TokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresOnUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresOnUtc);
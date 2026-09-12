namespace FocusLens.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public int TokenExpirationInMinutes { get; set; } = 15;

    public int RefreshTokenExpirationInDays { get; set; } = 30;
}
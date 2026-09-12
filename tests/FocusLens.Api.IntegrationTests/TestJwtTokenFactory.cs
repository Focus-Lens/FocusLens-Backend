using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FocusLens.Api.IntegrationTests;

public static class TestJwtTokenFactory
{
    private const string Secret = "FocusLens.IntegrationTests.Jwt.Secret.2026.1234567890abcdef";
    private const string Issuer = "FocusLens.Api";
    private const string Audience = "FocusLens.Client";

    public static string Create(Guid userId, string role)
    {
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role)
        ];

        SymmetricSecurityKey key =
            new(Encoding.UTF8.GetBytes(Secret));

        SigningCredentials credentials =
            new(key, SecurityAlgorithms.HmacSha256);

        JwtSecurityToken token = new(
            Issuer,
            Audience,
            claims,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(15),
            credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
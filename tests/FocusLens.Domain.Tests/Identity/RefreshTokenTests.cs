using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;

namespace FocusLens.Domain.Tests.Identity;

public class RefreshTokenTests
{
    [Fact]
    public void Create_WithValidValues_ReturnsActiveRefreshToken()
    {
        var expiresOnUtc = DateTimeOffset.UtcNow.AddDays(7);

        Result<RefreshToken> result = RefreshToken.Create(
            Guid.NewGuid(),
            " token-value ",
            Guid.NewGuid(),
            expiresOnUtc);

        Assert.True(result.IsSuccess);
        Assert.Equal("token-value", result.Value.Token);
        Assert.Equal(expiresOnUtc, result.Value.ExpiresOnUtc);
        Assert.Null(result.Value.RevokedOnUtc);
        Assert.False(result.Value.IsExpired);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public void Create_WithExpiredValue_ReturnsValidationError()
    {
        Result<RefreshToken> result = RefreshToken.Create(
            Guid.NewGuid(),
            "token-value",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.False(result.IsSuccess);
        Assert.Equal(RefreshTokenErrors.ExpiryInvalid, result.TopError);
    }

    [Fact]
    public void Revoke_WithValidValue_MarksTokenInactive()
    {
        RefreshToken token = RefreshToken.Create(
            Guid.NewGuid(),
            "token-value",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(7)).Value;
        var revokedOnUtc = DateTimeOffset.UtcNow;

        Result<Success> result = token.Revoke(revokedOnUtc);

        Assert.True(result.IsSuccess);
        Assert.Equal(revokedOnUtc, token.RevokedOnUtc);
        Assert.False(token.IsActive);
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_ReturnsConflictError()
    {
        RefreshToken token = RefreshToken.Create(
            Guid.NewGuid(),
            "token-value",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(7)).Value;

        token.Revoke(DateTimeOffset.UtcNow);

        Result<Success> result = token.Revoke(DateTimeOffset.UtcNow);

        Assert.False(result.IsSuccess);
        Assert.Equal(RefreshTokenErrors.AlreadyRevoked, result.TopError);
    }
}

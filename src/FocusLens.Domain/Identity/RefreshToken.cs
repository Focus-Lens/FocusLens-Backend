using FocusLens.Domain.Common;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Domain.Identity;

public sealed class RefreshToken : AuditableEntity
{
    private RefreshToken()
    {
    }

    private RefreshToken(
        Guid id,
        string token,
        Guid userId,
        DateTimeOffset expiresOnUtc)
        : base(id)
    {
        Token = token;
        UserId = userId;
        ExpiresOnUtc = expiresOnUtc;
    }

    public string Token { get; private set; } = string.Empty;

    public Guid UserId { get; private set; }

    public DateTimeOffset ExpiresOnUtc { get; }

    public DateTimeOffset? RevokedOnUtc { get; private set; }

    public bool IsExpired => ExpiresOnUtc <= DateTimeOffset.UtcNow;

    public bool IsActive => RevokedOnUtc is null && !IsExpired;

    public static Result<RefreshToken> Create(
        Guid id,
        string? token,
        Guid userId,
        DateTimeOffset expiresOnUtc)
    {
        if (id == Guid.Empty)
        {
            return RefreshTokenErrors.IdRequired;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return RefreshTokenErrors.TokenRequired;
        }

        if (userId == Guid.Empty)
        {
            return RefreshTokenErrors.UserIdRequired;
        }

        if (expiresOnUtc <= DateTimeOffset.UtcNow)
        {
            return RefreshTokenErrors.ExpiryInvalid;
        }

        return new RefreshToken(
            id,
            token.Trim(),
            userId,
            expiresOnUtc);
    }

    public Result<Success> Revoke(DateTimeOffset revokedOnUtc)
    {
        if (RevokedOnUtc is not null)
        {
            return RefreshTokenErrors.AlreadyRevoked;
        }

        if (revokedOnUtc > DateTimeOffset.UtcNow)
        {
            return RefreshTokenErrors.RevokedOnInvalid;
        }

        RevokedOnUtc = revokedOnUtc;

        return Result.Success;
    }

    public Result<Success> Revoke() => Revoke(DateTimeOffset.UtcNow);
}
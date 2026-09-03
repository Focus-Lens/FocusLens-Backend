using FocusLens.Domain.Common.Results;

namespace FocusLens.Domain.Identity;

public static class RefreshTokenErrors
{
    public static readonly Error IdRequired = Error.Validation(
        "RefreshToken_Id_Required",
        "Refresh token id is required.");

    public static readonly Error TokenRequired = Error.Validation(
        "RefreshToken_Token_Required",
        "Refresh token is required.");

    public static readonly Error UserIdRequired = Error.Validation(
        "RefreshToken_UserId_Required",
        "Refresh token user id is required.");

    public static readonly Error ExpiryInvalid = Error.Validation(
        "RefreshToken_Expiry_Invalid",
        "Refresh token expiry must be in the future.");

    public static readonly Error AlreadyRevoked = Error.Conflict(
        "RefreshToken_Already_Revoked",
        "Refresh token is already revoked.");

    public static readonly Error RevokedOnInvalid = Error.Validation(
        "RefreshToken_RevokedOn_Invalid",
        "Refresh token revocation time cannot be in the future.");
}

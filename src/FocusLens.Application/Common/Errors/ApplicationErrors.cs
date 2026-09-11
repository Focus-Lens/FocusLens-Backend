using FocusLens.Domain.Common.Results;

namespace FocusLens.Application.Common.Errors;

public static class ApplicationErrors
{
    public static class Identity
    {
        public static readonly Error InvalidCredentials = Error.Unauthorized(
            "Identity_Invalid_Credentials",
            "Invalid email or password.");

        public static readonly Error UserDisabled = Error.Forbidden(
            "Identity_User_Disabled",
            "This user account is disabled.");

        public static readonly Error UserLockedOut = Error.Forbidden(
            "Identity_User_Locked_Out",
            "This user account is locked.");

        public static readonly Error EmailNotConfirmed = Error.Forbidden(
            "Identity_Email_Not_Confirmed",
            "Email address has not been confirmed.");

        public static readonly Error EmailAlreadyConfirmed = Error.Conflict(
            "Identity_Email_Already_Confirmed",
            "Email address is already confirmed.");

        public static readonly Error EmailAlreadyRegistered = Error.Conflict(
            "Identity_Email_Already_Registered",
            "Email address is already registered.");

        public static readonly Error InvalidRefreshToken = Error.Unauthorized(
            "Identity_Invalid_Refresh_Token",
            "Refresh token is invalid or expired.");

        public static readonly Error InvalidExternalToken = Error.Unauthorized(
            "Identity_Invalid_External_Token",
            "External authentication token is invalid.");

        public static readonly Error ExternalEmailNotVerified = Error.Forbidden(
            "Identity_External_Email_Not_Verified",
            "External account email address is not verified.");

        public static Error OperationFailed(string description)
            => Error.Failure("Identity_Operation_Failed", description);
    }

    public static class Otp
    {
        public static readonly Error Invalid = Error.Validation(
            "Otp_Invalid",
            "Verification code is invalid.");

        public static readonly Error Expired = Error.Validation(
            "Otp_Expired",
            "Verification code has expired.");

        public static readonly Error AlreadyUsed = Error.Conflict(
            "Otp_Already_Used",
            "Verification code has already been used.");
    }

    public static class Users
    {
        public static readonly Error CurrentUserUnavailable = Error.Unauthorized(
            "Users_Current_User_Unavailable",
            "Current user is unavailable.");

        public static readonly Error NotFound = Error.NotFound(
            "Users_Not_Found",
            "User was not found.");
    }

    public static class Terms
    {
        public static readonly Error InvalidAudience = Error.Validation(
            "Terms_Invalid_Audience",
            "Terms audience must be Student or Parent.");

        public static readonly Error NotFound = Error.NotFound(
            "Terms_Not_Found",
            "Published terms were not found for the requested audience.");

        public static readonly Error DocumentNotFound = Error.NotFound(
            "Terms_Document_Not_Found",
            "Terms document was not found.");

        public static readonly Error DocumentNotPublished = Error.Validation(
            "Terms_Document_Not_Published",
            "Terms document is not published.");

        public static readonly Error CurrentUserUnavailable = Error.Unauthorized(
            "Terms_Current_User_Unavailable",
            "Current user is required to record terms acceptance.");
    }
}

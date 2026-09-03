namespace FocusLens.Application.Common.Models;

public sealed record EmailVerificationCodeValidationResult(
    EmailVerificationCodeValidationStatus Status,
    Guid? UserId = null);

public enum EmailVerificationCodeValidationStatus
{
    Valid,
    NotFound,
    Expired,
    Used
}

using FocusLens.Application.Common.Models;

namespace FocusLens.Application.Common.Interfaces;

public interface IEmailVerificationCodeStore
{
    Task SaveAsync(
        Guid userId,
        string email,
        string code,
        DateTimeOffset expiresOnUtc,
        CancellationToken cancellationToken = default,
        OtpCodePurpose purpose = OtpCodePurpose.EmailVerification);

    Task<EmailVerificationCodeValidationResult> ValidateAsync(
        string email,
        string code,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default,
        OtpCodePurpose purpose = OtpCodePurpose.EmailVerification,
        bool consume = true);

    Task ConsumeAsync(
        string email,
        string code,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default,
        OtpCodePurpose purpose = OtpCodePurpose.EmailVerification);
}
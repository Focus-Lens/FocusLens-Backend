using FocusLens.Application.Common.Models;

namespace FocusLens.Application.Common.Interfaces;

public interface IEmailVerificationCodeStore
{
    Task SaveAsync(
        Guid userId,
        string email,
        string code,
        DateTimeOffset expiresOnUtc,
        CancellationToken cancellationToken = default);

    Task<EmailVerificationCodeValidationResult> ValidateAsync(
        string email,
        string code,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);
}

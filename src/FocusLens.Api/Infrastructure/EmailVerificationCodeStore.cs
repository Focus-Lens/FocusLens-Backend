using System.Security.Cryptography;
using System.Text;

using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Common.Models;
using FocusLens.Infrastructure.Data;
using FocusLens.Infrastructure.Identity.Verification;
using Microsoft.EntityFrameworkCore;

namespace FocusLens.API.Infrastructure;

public sealed class EmailVerificationCodeStore : IEmailVerificationCodeStore
{
    private readonly ApplicationDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public EmailVerificationCodeStore(
        ApplicationDbContext dbContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task SaveAsync(
        Guid userId,
        string email,
        string code,
        DateTimeOffset expiresOnUtc,
        CancellationToken cancellationToken = default,
        OtpCodePurpose purpose = OtpCodePurpose.EmailVerification)
    {
        string normalizedEmail = NormalizeEmail(email);
        DateTimeOffset utcNow = _timeProvider.GetUtcNow();
        int purposeValue = (int)purpose;

        List<EmailVerificationCode> activeCodes = await _dbContext
            .EmailVerificationCodes
            .Where(storedCode => storedCode.Email == normalizedEmail
                && storedCode.Purpose == purposeValue
                && storedCode.UsedOnUtc == null
                && storedCode.ExpiresOnUtc > utcNow)
            .ToListAsync(cancellationToken);

        foreach (EmailVerificationCode activeCode in activeCodes)
        {
            activeCode.UsedOnUtc = utcNow;
        }

        _dbContext.EmailVerificationCodes.Add(new EmailVerificationCode
        {
            UserId = userId,
            Email = normalizedEmail,
            CodeHash = HashCode(normalizedEmail, code),
            Purpose = purposeValue,
            ExpiresOnUtc = expiresOnUtc,
            CreatedOnUtc = utcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<EmailVerificationCodeValidationResult> ValidateAsync(
        string email,
        string code,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default,
        OtpCodePurpose purpose = OtpCodePurpose.EmailVerification,
        bool consume = true)
    {
        string normalizedEmail = NormalizeEmail(email);
        string hash = HashCode(normalizedEmail, code);
        int purposeValue = (int)purpose;

        EmailVerificationCode? persistedCode = await _dbContext
            .EmailVerificationCodes
            .Where(storedCode => storedCode.Email == normalizedEmail
                && storedCode.Purpose == purposeValue
                && storedCode.CodeHash == hash)
            .OrderByDescending(storedCode => storedCode.CreatedOnUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (persistedCode is null)
        {
            return new EmailVerificationCodeValidationResult(
                EmailVerificationCodeValidationStatus.NotFound);
        }

        if (persistedCode.UsedOnUtc is not null)
        {
            return new EmailVerificationCodeValidationResult(
                EmailVerificationCodeValidationStatus.Used,
                persistedCode.UserId);
        }

        if (persistedCode.ExpiresOnUtc <= utcNow)
        {
            return new EmailVerificationCodeValidationResult(
                EmailVerificationCodeValidationStatus.Expired,
                persistedCode.UserId);
        }

        if (consume)
        {
            persistedCode.UsedOnUtc = utcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new EmailVerificationCodeValidationResult(
            EmailVerificationCodeValidationStatus.Valid,
            persistedCode.UserId);
    }

    public async Task ConsumeAsync(
        string email,
        string code,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default,
        OtpCodePurpose purpose = OtpCodePurpose.EmailVerification)
    {
        string normalizedEmail = NormalizeEmail(email);
        string hash = HashCode(normalizedEmail, code);
        int purposeValue = (int)purpose;

        EmailVerificationCode? persistedCode = await _dbContext
            .EmailVerificationCodes
            .Where(storedCode => storedCode.Email == normalizedEmail
                && storedCode.Purpose == purposeValue
                && storedCode.CodeHash == hash
                && storedCode.UsedOnUtc == null
                && storedCode.ExpiresOnUtc > utcNow)
            .OrderByDescending(storedCode => storedCode.CreatedOnUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (persistedCode is null)
        {
            return;
        }

        persistedCode.UsedOnUtc = utcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeEmail(string email)
        => email.Trim().ToUpperInvariant();

    private static string HashCode(string normalizedEmail, string code)
    {
        string valueToHash = string.Concat(normalizedEmail, ":", code);
        byte[] bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(valueToHash));

        return Convert.ToBase64String(bytes);
    }
}

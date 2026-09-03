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
    private readonly ApplicationDBContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public EmailVerificationCodeStore(
        ApplicationDBContext dbContext,
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
        CancellationToken cancellationToken = default)
    {
        string normalizedEmail = NormalizeEmail(email);
        DateTimeOffset utcNow = _timeProvider.GetUtcNow();

        List<EmailVerificationCode> activeCodes = await _dbContext
            .EmailVerificationCodes
            .Where(storedCode => storedCode.Email == normalizedEmail
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
            ExpiresOnUtc = expiresOnUtc,
            CreatedOnUtc = utcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<EmailVerificationCodeValidationResult> ValidateAsync(
        string email,
        string code,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        string normalizedEmail = NormalizeEmail(email);
        string hash = HashCode(normalizedEmail, code);

        EmailVerificationCode? persistedCode = await _dbContext
            .EmailVerificationCodes
            .Where(storedCode => storedCode.Email == normalizedEmail
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

        persistedCode.UsedOnUtc = utcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new EmailVerificationCodeValidationResult(
            EmailVerificationCodeValidationStatus.Valid,
            persistedCode.UserId);
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

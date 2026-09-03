using FocusLens.Application.Common.Interfaces;

namespace FocusLens.API.Infrastructure;

public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailVerificationCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Email verification code for {Email}: {Code}",
            email,
            code);

        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(
        string email,
        string resetToken,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Password reset token for {Email}: {ResetToken}",
            email,
            resetToken);

        return Task.CompletedTask;
    }
}

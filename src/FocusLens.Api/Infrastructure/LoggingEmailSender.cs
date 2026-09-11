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
        TimeSpan codeLifetime,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Email verification code sent to {Email} (expires in {CodeLifetime})",
            email,
            codeLifetime);

        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(
        string email,
        string code,
        TimeSpan codeLifetime,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Password reset code sent to {Email} (expires in {CodeLifetime})",
            email,
            codeLifetime);

        return Task.CompletedTask;
    }

    public Task SendParentStudentInvitationAsync(
        string studentEmail,
        string parentEmail,
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Parent-student invitation {InvitationId} sent to {StudentEmail} from {ParentEmail}",
            invitationId,
            studentEmail,
            parentEmail);

        return Task.CompletedTask;
    }

    public Task SendStudentParentInvitationAsync(
        string parentEmail,
        string studentDisplayName,
        string invitationUrl,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Student-parent invitation sent to {ParentEmail} from {StudentDisplayName}: {InvitationUrl}",
            parentEmail,
            studentDisplayName,
            invitationUrl);

        return Task.CompletedTask;
    }
}

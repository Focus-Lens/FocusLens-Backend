namespace FocusLens.Application.Common.Interfaces;

public interface IEmailSender
{
    Task SendEmailVerificationCodeAsync(
        string email,
        string code,
        TimeSpan codeLifetime,
        CancellationToken cancellationToken = default
    );

    Task SendPasswordResetAsync(
        string email,
        string code,
        TimeSpan codeLifetime,
        CancellationToken cancellationToken = default
    );

    Task SendParentStudentInvitationAsync(
        string studentEmail,
        string parentEmail,
        string invitationUrl,
        CancellationToken cancellationToken = default
    );

    Task SendStudentParentInvitationAsync(
        string parentEmail,
        string studentDisplayName,
        string invitationUrl,
        CancellationToken cancellationToken = default
    );

    Task SendChildSetupInvitationAsync(
        string childEmail,
        string parentName,
        string invitationUrl,
        CancellationToken cancellationToken = default
    );
}

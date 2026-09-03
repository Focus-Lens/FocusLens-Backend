namespace FocusLens.Application.Common.Interfaces;

public interface IEmailSender
{
    Task SendEmailVerificationCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetAsync(
        string email,
        string resetToken,
        CancellationToken cancellationToken = default);
}

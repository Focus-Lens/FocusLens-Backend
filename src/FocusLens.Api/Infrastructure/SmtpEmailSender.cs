using FocusLens.Application.Common.Interfaces;
using FocusLens.Settings;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FocusLens.API.Infrastructure;

public sealed class SmtpEmailSender : IEmailSender
{
    private const string EmailVerificationTemplate = "EmailVerification.html";
    private const string PasswordResetTemplate = "PasswordReset.html";

    private readonly MailSettings _settings;
    private readonly EmailTemplateRenderer _templateRenderer;

    public SmtpEmailSender(
        IOptions<MailSettings> settings,
        EmailTemplateRenderer templateRenderer)
    {
        _settings = settings.Value;
        _templateRenderer = templateRenderer;
    }

    public async Task SendEmailVerificationCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        string htmlBody = await _templateRenderer.RenderAsync(
            EmailVerificationTemplate,
            new Dictionary<string, string>
            {
                ["{{Email}}"] = email,
                ["{{Otp}}"] = code
            },
            cancellationToken);

        await SendAsync(
            email,
            "Verify your FocusLens email",
            htmlBody,
            cancellationToken);
    }

    public async Task SendPasswordResetAsync(
        string email,
        string resetToken,
        CancellationToken cancellationToken = default)
    {
        string htmlBody = await _templateRenderer.RenderAsync(
            PasswordResetTemplate,
            new Dictionary<string, string>
            {
                ["{{Email}}"] = email,
                ["{{ResetToken}}"] = resetToken
            },
            cancellationToken);

        await SendAsync(
            email,
            "Reset your FocusLens password",
            htmlBody,
            cancellationToken);
    }

    private async Task SendAsync(
        string email,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        MimeMessage message = new();
        message.From.Add(new MailboxAddress(
            _settings.DisplayName,
            _settings.Mail));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;
        message.Body = new BodyBuilder
        {
            HtmlBody = htmlBody
        }.ToMessageBody();

        using SmtpClient smtpClient = new();

        await smtpClient.ConnectAsync(
            _settings.Host,
            _settings.Port,
            SecureSocketOptions.StartTls,
            cancellationToken);

        await smtpClient.AuthenticateAsync(
            _settings.Mail,
            _settings.Password,
            cancellationToken);

        await smtpClient.SendAsync(message, cancellationToken);

        await smtpClient.DisconnectAsync(true, cancellationToken);
    }
}

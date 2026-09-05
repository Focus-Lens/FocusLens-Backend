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
    private const string ParentStudentInvitationTemplate = "ParentStudentInvitation.html";

    private readonly MailSettings _settings;
    private readonly EmailTemplateRenderer _templateRenderer;
    private readonly InvitationSettings _invitationSettings;

    public SmtpEmailSender(
        IOptions<MailSettings> settings,
        EmailTemplateRenderer templateRenderer,
        IOptions<InvitationSettings> invitationSettings)
    {
        _settings = settings.Value;
        _templateRenderer = templateRenderer;
        _invitationSettings = invitationSettings.Value;
    }

    public async Task SendEmailVerificationCodeAsync(
        string email,
        string code,
        TimeSpan codeLifetime,
        CancellationToken cancellationToken = default)
    {
        string htmlBody = await _templateRenderer.RenderAsync(
            EmailVerificationTemplate,
            new Dictionary<string, string>
            {
                ["{{Email}}"] = email,
                ["{{Otp}}"] = code,
                ["{{ExpiryMinutes}}"] = ((int)codeLifetime.TotalMinutes).ToString()
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
        string code,
        TimeSpan codeLifetime,
        CancellationToken cancellationToken = default)
    {
        string htmlBody = await _templateRenderer.RenderAsync(
            PasswordResetTemplate,
            new Dictionary<string, string>
            {
                ["{{Email}}"] = email,
                ["{{Otp}}"] = code,
                ["{{ExpiryMinutes}}"] = ((int)codeLifetime.TotalMinutes).ToString()
            },
            cancellationToken);

        await SendAsync(
            email,
            "Reset your FocusLens password",
            htmlBody,
            cancellationToken);
    }

    public async Task SendParentStudentInvitationAsync(
        string studentEmail,
        string parentEmail,
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        string invitationUrl = $"{_invitationSettings.BaseUrl.TrimEnd('/')}/{invitationId}";

        string htmlBody = await _templateRenderer.RenderAsync(
            ParentStudentInvitationTemplate,
            new Dictionary<string, string>
            {
                ["{{ParentEmail}}"] = parentEmail,
                ["{{InvitationUrl}}"] = invitationUrl
            },
            cancellationToken);

        await SendAsync(
            studentEmail,
            "You have a FocusLens invitation",
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

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = htmlBody
        };

        var logoPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "wwwroot",
            "images",
            "focuslens-logo.png"
        );

        var mascotPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "wwwroot",
            "images",
            "focuslens-mascot.png"
        );

        var logo = bodyBuilder.LinkedResources.Add(logoPath);
        logo.ContentId = "focuslens-logo";
        logo.ContentDisposition =
            new ContentDisposition(ContentDisposition.Inline);

        var mascot = bodyBuilder.LinkedResources.Add(mascotPath);
        mascot.ContentId = "focuslens-mascot";
        mascot.ContentDisposition =
            new ContentDisposition(ContentDisposition.Inline);

        message.Body = bodyBuilder.ToMessageBody();

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

        await smtpClient.SendAsync(
            message,
            cancellationToken);

        await smtpClient.DisconnectAsync(
            true,
            cancellationToken);
    }
}

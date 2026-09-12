using System.Net;
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
    private const string ChildSetupInvitationTemplate = "ChildSetupInvitation.html";
    private readonly InvitationSettings _invitationSettings;

    private readonly MailSettings _settings;
    private readonly EmailTemplateRenderer _templateRenderer;

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
            new Dictionary<string, string> { ["{{ParentEmail}}"] = parentEmail, ["{{InvitationUrl}}"] = invitationUrl },
            cancellationToken);

        await SendAsync(
            studentEmail,
            "You have a FocusLens invitation",
            htmlBody,
            cancellationToken);
    }

    public Task SendStudentParentInvitationAsync(
        string parentEmail,
        string studentDisplayName,
        string invitationUrl,
        CancellationToken cancellationToken = default)
    {
        string encodedName = WebUtility.HtmlEncode(studentDisplayName);
        string encodedUrl = WebUtility.HtmlEncode(invitationUrl);
        string htmlBody = $"""
                           <p>{encodedName} invited you to connect on FocusLens.</p>
                           <p><a href=\"{encodedUrl}\">Review invitation</a></p>
                           <p>This invitation expires in 7 days.</p>
                           """;

        return SendAsync(
            parentEmail,
            "You have a FocusLens invitation",
            htmlBody,
            cancellationToken);
    }

    public async Task SendChildSetupInvitationAsync(
        string childEmail,
        string invitationUrl,
        CancellationToken cancellationToken = default)
    {
        string htmlBody = await _templateRenderer.RenderAsync(
            ChildSetupInvitationTemplate,
            new Dictionary<string, string> { ["{{InvitationUrl}}"] = invitationUrl },
            cancellationToken);

        await SendAsync(
            childEmail,
            "Your FocusLens setup is ready",
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

        BodyBuilder bodyBuilder = new() { HtmlBody = htmlBody };

        string logoPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "wwwroot",
            "images",
            "focuslens-logo.png"
        );

        string mascotPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "wwwroot",
            "images",
            "focuslens-mascot.png"
        );

        MimeEntity logo = bodyBuilder.LinkedResources.Add(logoPath);
        logo.ContentId = "focuslens-logo";
        logo.ContentDisposition =
            new ContentDisposition(ContentDisposition.Inline);

        MimeEntity mascot = bodyBuilder.LinkedResources.Add(mascotPath);
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
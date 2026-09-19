using System.Net;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Settings;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IOptions<MailSettings> settings,
        EmailTemplateRenderer templateRenderer,
        IOptions<InvitationSettings> invitationSettings,
        ILogger<SmtpEmailSender> logger)
    {
        _settings = settings.Value;
        _templateRenderer = templateRenderer;
        _invitationSettings = invitationSettings.Value;
        _logger = logger;
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
        string invitationUrl =
            $"{_invitationSettings.BaseUrl.TrimEnd('/')}/{invitationId}";

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
            new Dictionary<string, string>
            {
                ["{{InvitationUrl}}"] = invitationUrl
            },
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
        try
        {
            Console.WriteLine(
                $"SMTP: Starting email send to {email}");

            _logger.LogInformation(
                "SMTP: Starting email send to {Email}",
                email);

            MimeMessage message = new();

            message.From.Add(new MailboxAddress(
                _settings.DisplayName,
                _settings.Mail));

            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = subject;

            BodyBuilder bodyBuilder = new()
            {
                HtmlBody = htmlBody
            };

            string logoPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "images",
                "focuslens-logo.png");

            string mascotPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "images",
                "focuslens-mascot.png");

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

            Console.WriteLine(
                $"SMTP: Connecting to {_settings.Host}:{_settings.Port}");

            _logger.LogInformation(
                "SMTP: Connecting to {Host}:{Port}",
                _settings.Host,
                _settings.Port);

            await smtpClient.ConnectAsync(
                _settings.Host,
                _settings.Port,
                SecureSocketOptions.StartTls,
                cancellationToken);

            Console.WriteLine("SMTP: Connected successfully");

            _logger.LogInformation(
                "SMTP: Connected successfully");

            Console.WriteLine("SMTP: Authenticating");

            _logger.LogInformation(
                "SMTP: Authenticating");

            await smtpClient.AuthenticateAsync(
                _settings.Mail,
                _settings.Password,
                cancellationToken);

            Console.WriteLine("SMTP: Authenticated successfully");

            _logger.LogInformation(
                "SMTP: Authenticated successfully");

            Console.WriteLine("SMTP: Sending email");

            _logger.LogInformation(
                "SMTP: Sending email");

            await smtpClient.SendAsync(
                message,
                cancellationToken);

            Console.WriteLine("SMTP: Email sent successfully");

            _logger.LogInformation(
                "SMTP: Email sent successfully");

            await smtpClient.DisconnectAsync(
                true,
                cancellationToken);

            Console.WriteLine("SMTP: Disconnected successfully");

            _logger.LogInformation(
                "SMTP: Disconnected successfully");
        }
        catch (OperationCanceledException ex)
        {
            Console.WriteLine(
                $"SMTP: Operation cancelled. {ex.Message}");

            _logger.LogError(
                ex,
                "SMTP: Operation cancelled while sending email");

            throw;
        }
        catch (SmtpCommandException ex)
        {
            Console.WriteLine(
                $"SMTP: Command error. StatusCode={ex.StatusCode}, Message={ex.Message}");

            _logger.LogError(
                ex,
                "SMTP: Command error. StatusCode={StatusCode}",
                ex.StatusCode);

            throw;
        }
        catch (SmtpProtocolException ex)
        {
            Console.WriteLine(
                $"SMTP: Protocol error. {ex.Message}");

            _logger.LogError(
                ex,
                "SMTP: Protocol error");

            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"SMTP: ERROR. Type={ex.GetType().Name}, Message={ex.Message}");

            _logger.LogError(
                ex,
                "SMTP: Unexpected error while sending email");

            throw;
        }
    }
}

// using System.Net;
// using FocusLens.Application.Common.Interfaces;
// using FocusLens.Settings;
// using MailKit.Net.Smtp;
// using MailKit.Security;
// using Microsoft.Extensions.Options;
// using MimeKit;

// namespace FocusLens.API.Infrastructure;

// public sealed class SmtpEmailSender : IEmailSender
// {
//     private const string EmailVerificationTemplate = "EmailVerification.html";
//     private const string PasswordResetTemplate = "PasswordReset.html";
//     private const string ParentStudentInvitationTemplate = "ParentStudentInvitation.html";
//     private const string ChildSetupInvitationTemplate = "ChildSetupInvitation.html";
//     private readonly InvitationSettings _invitationSettings;

//     private readonly MailSettings _settings;
//     private readonly EmailTemplateRenderer _templateRenderer;

//     public SmtpEmailSender(
//         IOptions<MailSettings> settings,
//         EmailTemplateRenderer templateRenderer,
//         IOptions<InvitationSettings> invitationSettings)
//     {
//         _settings = settings.Value;
//         _templateRenderer = templateRenderer;
//         _invitationSettings = invitationSettings.Value;
//     }

//     public async Task SendEmailVerificationCodeAsync(
//         string email,
//         string code,
//         TimeSpan codeLifetime,
//         CancellationToken cancellationToken = default)
//     {
//         string htmlBody = await _templateRenderer.RenderAsync(
//             EmailVerificationTemplate,
//             new Dictionary<string, string>
//             {
//                 ["{{Email}}"] = email,
//                 ["{{Otp}}"] = code,
//                 ["{{ExpiryMinutes}}"] = ((int)codeLifetime.TotalMinutes).ToString()
//             },
//             cancellationToken);

//         await SendAsync(
//             email,
//             "Verify your FocusLens email",
//             htmlBody,
//             cancellationToken);
//     }

//     public async Task SendPasswordResetAsync(
//         string email,
//         string code,
//         TimeSpan codeLifetime,
//         CancellationToken cancellationToken = default)
//     {
//         string htmlBody = await _templateRenderer.RenderAsync(
//             PasswordResetTemplate,
//             new Dictionary<string, string>
//             {
//                 ["{{Email}}"] = email,
//                 ["{{Otp}}"] = code,
//                 ["{{ExpiryMinutes}}"] = ((int)codeLifetime.TotalMinutes).ToString()
//             },
//             cancellationToken);

//         await SendAsync(
//             email,
//             "Reset your FocusLens password",
//             htmlBody,
//             cancellationToken);
//     }

//     public async Task SendParentStudentInvitationAsync(
//         string studentEmail,
//         string parentEmail,
//         Guid invitationId,
//         CancellationToken cancellationToken = default)
//     {
//         string invitationUrl = $"{_invitationSettings.BaseUrl.TrimEnd('/')}/{invitationId}";

//         string htmlBody = await _templateRenderer.RenderAsync(
//             ParentStudentInvitationTemplate,
//             new Dictionary<string, string> { ["{{ParentEmail}}"] = parentEmail, ["{{InvitationUrl}}"] = invitationUrl },
//             cancellationToken);

//         await SendAsync(
//             studentEmail,
//             "You have a FocusLens invitation",
//             htmlBody,
//             cancellationToken);
//     }

//     public Task SendStudentParentInvitationAsync(
//         string parentEmail,
//         string studentDisplayName,
//         string invitationUrl,
//         CancellationToken cancellationToken = default)
//     {
//         string encodedName = WebUtility.HtmlEncode(studentDisplayName);
//         string encodedUrl = WebUtility.HtmlEncode(invitationUrl);
//         string htmlBody = $"""
//                            <p>{encodedName} invited you to connect on FocusLens.</p>
//                            <p><a href=\"{encodedUrl}\">Review invitation</a></p>
//                            <p>This invitation expires in 7 days.</p>
//                            """;

//         return SendAsync(
//             parentEmail,
//             "You have a FocusLens invitation",
//             htmlBody,
//             cancellationToken);
//     }

//     public async Task SendChildSetupInvitationAsync(
//         string childEmail,
//         string invitationUrl,
//         CancellationToken cancellationToken = default)
//     {
//         string htmlBody = await _templateRenderer.RenderAsync(
//             ChildSetupInvitationTemplate,
//             new Dictionary<string, string> { ["{{InvitationUrl}}"] = invitationUrl },
//             cancellationToken);

//         await SendAsync(
//             childEmail,
//             "Your FocusLens setup is ready",
//             htmlBody,
//             cancellationToken);
//     }

//     private async Task SendAsync(
//         string email,
//         string subject,
//         string htmlBody,
//         CancellationToken cancellationToken)
//     {
//         MimeMessage message = new();

//         message.From.Add(new MailboxAddress(
//             _settings.DisplayName,
//             _settings.Mail));

//         message.To.Add(MailboxAddress.Parse(email));
//         message.Subject = subject;

//         BodyBuilder bodyBuilder = new() { HtmlBody = htmlBody };

//         string logoPath = Path.Combine(
//             Directory.GetCurrentDirectory(),
//             "wwwroot",
//             "images",
//             "focuslens-logo.png"
//         );

//         string mascotPath = Path.Combine(
//             Directory.GetCurrentDirectory(),
//             "wwwroot",
//             "images",
//             "focuslens-mascot.png"
//         );

//         MimeEntity logo = bodyBuilder.LinkedResources.Add(logoPath);
//         logo.ContentId = "focuslens-logo";
//         logo.ContentDisposition =
//             new ContentDisposition(ContentDisposition.Inline);

//         MimeEntity mascot = bodyBuilder.LinkedResources.Add(mascotPath);
//         mascot.ContentId = "focuslens-mascot";
//         mascot.ContentDisposition =
//             new ContentDisposition(ContentDisposition.Inline);

//         message.Body = bodyBuilder.ToMessageBody();

//         using SmtpClient smtpClient = new();

//         await smtpClient.ConnectAsync(
//             _settings.Host,
//             _settings.Port,
//             SecureSocketOptions.StartTls,
//             cancellationToken);

//         await smtpClient.AuthenticateAsync(
//             _settings.Mail,
//             _settings.Password,
//             cancellationToken);

//         await smtpClient.SendAsync(
//             message,
//             cancellationToken);

//         await smtpClient.DisconnectAsync(
//             true,
//             cancellationToken);
//     }
// }

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public SmtpEmailSender(
        IOptions<MailSettings> settings,
        EmailTemplateRenderer templateRenderer,
        IOptions<InvitationSettings> invitationSettings,
        ILogger<SmtpEmailSender> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _settings = settings.Value;
        _templateRenderer = templateRenderer;
        _invitationSettings = invitationSettings.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
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
                ["{{ExpiryMinutes}}"] =
                    ((int)codeLifetime.TotalMinutes).ToString()
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
                ["{{ExpiryMinutes}}"] =
                    ((int)codeLifetime.TotalMinutes).ToString()
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
                           <p><a href="{encodedUrl}">Review invitation</a></p>
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
            string? apiKey =
                _configuration["Resend:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "Resend:ApiKey is not configured.");
            }

            Console.WriteLine(
                $"RESEND: Starting email send to {email}");

            _logger.LogInformation(
                "RESEND: Starting email send to {Email}",
                email);

            using HttpClient client =
                _httpClientFactory.CreateClient();

            client.BaseAddress =
                new Uri("https://api.resend.com");

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    apiKey);

            string from =
                _configuration["Resend:From"]
                ?? "FocusLens <onboarding@resend.dev>";

            var requestBody = new
            {
                from,
                to = new[] { email },
                subject,
                html = htmlBody
            };

            string json =
                JsonSerializer.Serialize(requestBody);

            using StringContent content =
                new(
                    json,
                    Encoding.UTF8,
                    "application/json");

            Console.WriteLine(
                "RESEND: Sending request to Resend API");

            _logger.LogInformation(
                "RESEND: Sending request to Resend API");

            using HttpResponseMessage response =
                await client.PostAsync(
                    "/emails",
                    content,
                    cancellationToken);

            string responseBody =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(
                    $"RESEND: ERROR. Status={(int)response.StatusCode}, Response={responseBody}");

                _logger.LogError(
                    "RESEND: Failed. Status={StatusCode}, Response={Response}",
                    response.StatusCode,
                    responseBody);

                throw new InvalidOperationException(
                    $"Resend API failed with status {(int)response.StatusCode}: {responseBody}");
            }

            Console.WriteLine(
                $"RESEND: Email sent successfully. Response={responseBody}");

            _logger.LogInformation(
                "RESEND: Email sent successfully");

        }
        catch (OperationCanceledException ex)
        {
            Console.WriteLine(
                $"RESEND: Operation cancelled. {ex.Message}");

            _logger.LogError(
                ex,
                "RESEND: Operation cancelled");

            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"RESEND: ERROR. Type={ex.GetType().Name}, Message={ex.Message}");

            _logger.LogError(
                ex,
                "RESEND: Unexpected error while sending email");

            throw;
        }
    }
}

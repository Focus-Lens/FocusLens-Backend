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

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FocusLens.API.Infrastructure;

public sealed class SmtpEmailSender : IEmailSender
{
    private const string EmailVerificationTemplate = "EmailVerification.html";
    private const string PasswordResetTemplate = "PasswordReset.html";
    private const string ParentStudentInvitationTemplate = "ParentStudentInvitation.html";
    private const string StudentParentInvitationTemplate = "StudentParentInvitation.html";
    private const string ChildSetupInvitationTemplate = "ChildSetupInvitation.html";

    private readonly EmailTemplateRenderer _templateRenderer;
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public SmtpEmailSender(
        EmailTemplateRenderer templateRenderer,
        ILogger<SmtpEmailSender> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _templateRenderer = templateRenderer;
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
        string invitationUrl,
        CancellationToken cancellationToken = default)
    {
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

    public async Task SendStudentParentInvitationAsync(
        string parentEmail,
        string studentDisplayName,
        string invitationUrl,
        CancellationToken cancellationToken = default)
    {
        string htmlBody = await _templateRenderer.RenderAsync(
            StudentParentInvitationTemplate,
            new Dictionary<string, string>
            {
                ["{{StudentDisplayName}}"] = studentDisplayName,
                ["{{InvitationUrl}}"] = invitationUrl
            },
            cancellationToken);

        await SendAsync(
            parentEmail,
            "You have a FocusLens invitation",
            htmlBody,
            cancellationToken);
    }

    public async Task SendChildSetupInvitationAsync(
        string childEmail,
        string parentName,
        string invitationUrl,
        CancellationToken cancellationToken = default)
    {
        string htmlBody = await _templateRenderer.RenderAsync(
            ChildSetupInvitationTemplate,
            new Dictionary<string, string>
            {
                ["{{ParentName}}"] = parentName,
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
                $"GMAIL API: Starting email send to {email}");

            _logger.LogInformation(
                "GMAIL API: Starting email send to {Email}",
                email);

            string accessToken =
                await GetAccessTokenAsync(cancellationToken);

            string senderEmail =
                _configuration["Gmail:SenderEmail"]
                ?? throw new InvalidOperationException(
                    "Gmail:SenderEmail is not configured.");

            MimeMessage message = new();

            message.From.Add(
                new MailboxAddress(
                    "FocusLens",
                    senderEmail));

            message.To.Add(
                MailboxAddress.Parse(email));

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

            if (File.Exists(logoPath))
            {
                MimeEntity logo =
                    bodyBuilder.LinkedResources.Add(logoPath);

                logo.ContentId = "focuslens-logo";

                logo.ContentDisposition =
                    new ContentDisposition(
                        ContentDisposition.Inline);
            }

            if (File.Exists(mascotPath))
            {
                MimeEntity mascot =
                    bodyBuilder.LinkedResources.Add(mascotPath);

                mascot.ContentId = "focuslens-mascot";

                mascot.ContentDisposition =
                    new ContentDisposition(
                        ContentDisposition.Inline);
            }

            message.Body = bodyBuilder.ToMessageBody();

            using MemoryStream stream = new();

            await message.WriteToAsync(
                stream,
                cancellationToken);

            string rawMessage =
                Convert.ToBase64String(stream.ToArray())
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .TrimEnd('=');

            using HttpClient client =
                _httpClientFactory.CreateClient();

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    accessToken);

            var requestBody = new
            {
                raw = rawMessage
            };

            string json =
                JsonSerializer.Serialize(requestBody);

            using StringContent content =
                new(
                    json,
                    Encoding.UTF8,
                    "application/json");

            Console.WriteLine(
                "GMAIL API: Sending HTTPS request");

            _logger.LogInformation(
                "GMAIL API: Sending HTTPS request");

            using HttpResponseMessage response =
                await client.PostAsync(
                    "https://gmail.googleapis.com/gmail/v1/users/me/messages/send",
                    content,
                    cancellationToken);

            string responseBody =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(
                    $"GMAIL API: ERROR. Status={(int)response.StatusCode}, Response={responseBody}");

                _logger.LogError(
                    "GMAIL API: Failed. Status={StatusCode}, Response={Response}",
                    response.StatusCode,
                    responseBody);

                throw new InvalidOperationException(
                    $"Gmail API failed with status {(int)response.StatusCode}: {responseBody}");
            }

            Console.WriteLine(
                $"GMAIL API: Email sent successfully. Response={responseBody}");

            _logger.LogInformation(
                "GMAIL API: Email sent successfully");
        }
        catch (OperationCanceledException ex)
        {
            Console.WriteLine(
                $"GMAIL API: Operation cancelled. {ex.Message}");

            _logger.LogError(
                ex,
                "GMAIL API: Operation cancelled");

            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"GMAIL API: ERROR. Type={ex.GetType().Name}, Message={ex.Message}");

            _logger.LogError(
                ex,
                "GMAIL API: Unexpected error");

            throw;
        }
    }

    private async Task<string> GetAccessTokenAsync(
        CancellationToken cancellationToken)
    {
        string clientId =
            _configuration["Gmail:ClientId"]
            ?? throw new InvalidOperationException(
                "Gmail:ClientId is not configured.");

        string clientSecret =
            _configuration["Gmail:ClientSecret"]
            ?? throw new InvalidOperationException(
                "Gmail:ClientSecret is not configured.");

        string refreshToken =
            _configuration["Gmail:RefreshToken"]
            ?? throw new InvalidOperationException(
                "Gmail:RefreshToken is not configured.");

        using HttpClient client =
            _httpClientFactory.CreateClient();

        using FormUrlEncodedContent content =
            new(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            });

        Console.WriteLine(
            "GMAIL API: Requesting access token");

        using HttpResponseMessage response =
            await client.PostAsync(
                "https://oauth2.googleapis.com/token",
                content,
                cancellationToken);

        string responseBody =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Google token endpoint failed with status {(int)response.StatusCode}: {responseBody}");
        }

        using JsonDocument document =
            JsonDocument.Parse(responseBody);

        if (!document.RootElement.TryGetProperty(
                "access_token",
                out JsonElement accessTokenElement))
        {
            throw new InvalidOperationException(
                "Google token response did not contain an access_token.");
        }

        return accessTokenElement.GetString()
            ?? throw new InvalidOperationException(
                "Google returned an empty access_token.");
    }
}

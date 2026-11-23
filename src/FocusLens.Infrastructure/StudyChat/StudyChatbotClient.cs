using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FocusLens.Application.StudyChat;
using Microsoft.Extensions.Options;

namespace FocusLens.Infrastructure.StudyChat;

public sealed class StudyChatbotClient(HttpClient client, IOptions<StudyChatbotOptions> options) : IStudyChatbotClient
{
    public async Task StartSessionAsync(ChatSessionStart request, CancellationToken ct)
    {
        using HttpRequestMessage message = CreateRequest("chat/session", new { session_id = request.AiSessionId, material_text = request.MaterialText, subject = request.Subject, lesson_title = request.LessonTitle, student = new { grade = request.Grade } });
        using HttpResponseMessage response = await SendRequestAsync(message, ct);
        await EnsureSuccessAsync(response, ct);
    }
    public async Task<ChatReply> SendAsync(string message, string? aiSessionId, IReadOnlyList<TemporaryChatMessage> history, string? materialText, string? subject, CancellationToken ct)
    {
        var turns = history.TakeLast(6).Select(x => new { role = x.Role, text = x.Text });
        using HttpRequestMessage request = CreateRequest("chat/message", new { message, session_id = aiSessionId ?? "", history = turns, material_text = materialText ?? "", subject = subject ?? "" });
        using HttpResponseMessage response = await SendRequestAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        ChatReplyDto dto = await response.Content.ReadFromJsonAsync<ChatReplyDto>(cancellationToken: ct) ?? throw new StudyChatRemoteException(HttpStatusCode.BadGateway, "Invalid chatbot response.");
        return new(dto.Reply ?? "", dto.Category ?? "other", dto.Blocked, dto.Reason, dto.Pages ?? [], dto.Usage?.Input ?? 0, dto.Usage?.Output ?? 0, dto.Usage?.Total ?? 0);
    }
    public async Task<string> ExtractTextAsync(string contentType, byte[] content, CancellationToken ct)
    {
        using HttpRequestMessage request = CreateRequest("ocr", new { mime_type = contentType, data = Convert.ToBase64String(content) });
        using HttpResponseMessage response = await SendRequestAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        OcrDto dto = await response.Content.ReadFromJsonAsync<OcrDto>(cancellationToken: ct) ?? throw new StudyChatRemoteException(HttpStatusCode.BadGateway, "Invalid OCR response.");
        return dto.Text ?? "";
    }
    public async Task EndSessionAsync(string aiSessionId, CancellationToken ct)
    { using HttpRequestMessage request = CreateRequest("chat/session/end", new { session_id = aiSessionId }); using HttpResponseMessage response = await SendRequestAsync(request, ct); if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound) await EnsureSuccessAsync(response, ct); }
    private HttpRequestMessage CreateRequest(string path, object body)
    {
        StudyChatbotOptions configured = options.Value;
        if (!Uri.TryCreate(configured.BaseUrl, UriKind.Absolute, out Uri? baseUri) || string.IsNullOrWhiteSpace(configured.ServiceKey))
            throw new InvalidOperationException("StudyChatbot:BaseUrl and StudyChatbot:ServiceKey must be configured before using chat.");
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, path)) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Service-Key", configured.ServiceKey);
        return request;
    }
    private async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await client.SendAsync(request, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // HttpClient uses OperationCanceledException for its own timeout. Preserve a caller's
            // cancellation so a disconnected client is not reported as an upstream timeout.
            throw new StudyChatTimeoutException();
        }
    }
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    { if (response.IsSuccessStatusCode) return; string detail = await response.Content.ReadAsStringAsync(ct); throw new StudyChatRemoteException(response.StatusCode, string.IsNullOrWhiteSpace(detail) ? "Study chatbot is unavailable." : "Study chatbot request failed."); }
    private sealed class OcrDto { public string? Text { get; init; } }
    private sealed class UsageDto { public int Input { get; init; } public int Output { get; init; } public int Total { get; init; } }
    private sealed class ChatReplyDto { public string? Reply { get; init; } public string? Category { get; init; } public bool Blocked { get; init; } public string? Reason { get; init; } public List<int>? Pages { get; init; } public UsageDto? Usage { get; init; } }
}
public sealed class StudyChatRemoteException(HttpStatusCode statusCode, string message) : Exception(message) { public HttpStatusCode StatusCode { get; } = statusCode; }
public sealed class StudyChatTimeoutException : Exception
{
    public StudyChatTimeoutException() : base("Study chatbot did not respond in time.") { }
}

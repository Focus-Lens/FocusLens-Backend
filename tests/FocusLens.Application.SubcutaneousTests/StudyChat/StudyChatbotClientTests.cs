using System.Net;
using System.Text;
using FocusLens.Application.StudyChat;
using FocusLens.Infrastructure.StudyChat;
using Microsoft.Extensions.Options;

namespace FocusLens.Application.SubcutaneousTests.StudyChat;

public sealed class StudyChatbotClientTests
{
    [Fact]
    public async Task Sequential_requests_use_per_request_authentication_without_mutating_http_client()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        var client = new StudyChatbotClient(httpClient, Options.Create(new StudyChatbotOptions
        {
            BaseUrl = "https://chatbot.test/",
            ServiceKey = "test-service-key"
        }));

        await client.StartSessionAsync(new("session-1", "material"), CancellationToken.None);
        await client.SendAsync("question", "session-1", [], null, null, CancellationToken.None);
        await client.ExtractTextAsync("application/pdf", "%PDF-"u8.ToArray(), CancellationToken.None);
        await client.EndSessionAsync("session-1", CancellationToken.None);

        Assert.Equal(["/chat/session", "/chat/message", "/ocr", "/chat/session/end"], handler.Paths);
        Assert.All(handler.ServiceKeys, key => Assert.Equal("test-service-key", key));
        Assert.Null(httpClient.BaseAddress);
        Assert.False(httpClient.DefaultRequestHeaders.Contains("X-Service-Key"));
    }

    [Fact]
    public async Task Missing_configuration_is_reported_only_when_a_chat_request_is_made()
    {
        using var httpClient = new HttpClient(new RecordingHandler());
        var client = new StudyChatbotClient(httpClient, Options.Create(new StudyChatbotOptions()));

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.StartSessionAsync(new("session-1", "material"), CancellationToken.None));

        Assert.Equal("StudyChatbot:BaseUrl and StudyChatbot:ServiceKey must be configured before using chat.", error.Message);
    }

    [Fact]
    public void Default_timeout_is_ninety_seconds()
    {
        Assert.Equal(90, new StudyChatbotOptions().TimeoutSeconds);
    }

    [Fact]
    public async Task Internal_http_client_timeout_is_mapped_once_without_retrying()
    {
        var handler = new DelayingHandler();
        using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(25) };
        var client = new StudyChatbotClient(httpClient, Options.Create(new StudyChatbotOptions
        {
            BaseUrl = "https://chatbot.test/",
            ServiceKey = "test-service-key"
        }));

        await Assert.ThrowsAsync<StudyChatTimeoutException>(() =>
            client.SendAsync("question", "session-1", [], null, null, CancellationToken.None));

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_mapped_to_a_chatbot_timeout()
    {
        var handler = new DelayingHandler();
        using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(1) };
        var client = new StudyChatbotClient(httpClient, Options.Create(new StudyChatbotOptions
        {
            BaseUrl = "https://chatbot.test/",
            ServiceKey = "test-service-key"
        }));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Exception error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.SendAsync("question", "session-1", [], null, null, cancellation.Token));

        Assert.IsNotType<StudyChatTimeoutException>(error);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public List<string?> ServiceKeys { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            ServiceKeys.Add(request.Headers.TryGetValues("X-Service-Key", out IEnumerable<string>? values) ? values.Single() : null);
            string json = request.RequestUri.AbsolutePath switch
            {
                "/chat/message" => "{\"reply\":\"answer\",\"category\":\"study\",\"blocked\":false,\"pages\":[],\"usage\":{\"input\":1,\"output\":2,\"total\":3}}",
                "/ocr" => "{\"text\":\"OCR text\"}",
                _ => "{}"
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class DelayingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The request should have been cancelled.");
        }
    }
}

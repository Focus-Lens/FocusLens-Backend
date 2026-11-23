using FocusLens.API.Infrastructure;
using FocusLens.Infrastructure.StudyChat;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FocusLens.Api.IntegrationTests;

public sealed class StudyChatbotTimeoutMiddlewareTests
{
    [Fact]
    public async Task Internal_chatbot_timeout_returns_safe_gateway_timeout_problem()
    {
        var context = new DefaultHttpContext();
        using ServiceProvider services = new ServiceCollection().AddLogging().BuildServiceProvider();
        context.RequestServices = services;
        context.Response.Body = new MemoryStream();
        var middleware = new StudyChatbotTimeoutMiddleware(_ =>
            throw new StudyChatTimeoutException());

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status504GatewayTimeout, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var body = new StreamReader(context.Response.Body);
        string json = await body.ReadToEndAsync();
        Assert.Contains("Study chatbot timed out", json);
        Assert.DoesNotContain("StudyChatTimeoutException", json);
    }

    [Fact]
    public async Task Request_cancellation_is_not_converted_to_gateway_timeout()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new DefaultHttpContext { RequestAborted = cancellation.Token };
        var middleware = new StudyChatbotTimeoutMiddleware(_ =>
            throw new StudyChatTimeoutException());

        await Assert.ThrowsAsync<StudyChatTimeoutException>(() => middleware.InvokeAsync(context));
        Assert.NotEqual(StatusCodes.Status504GatewayTimeout, context.Response.StatusCode);
    }
}

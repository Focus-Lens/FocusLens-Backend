using FocusLens.API.Infrastructure;
using FocusLens.Infrastructure.StudyChat;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FocusLens.Api.IntegrationTests;

public sealed class StudyChatbotTimeoutMiddlewareTests
{
    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(404)]
    public async Task Study_chat_exception_preserves_its_status_code(int statusCode)
    {
        var context = NewContext();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        bool handled = await handler.TryHandleAsync(context, new StudyChatException(statusCode, "safe message"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(statusCode, context.Response.StatusCode);
        string body = await ReadBodyAsync(context);
        Assert.Contains("safe message", body);
        Assert.Contains(context.TraceIdentifier, body);
    }

    [Theory]
    [InlineData(System.Net.HttpStatusCode.Unauthorized, StatusCodes.Status502BadGateway)]
    [InlineData(System.Net.HttpStatusCode.Forbidden, StatusCodes.Status502BadGateway)]
    [InlineData(System.Net.HttpStatusCode.BadRequest, StatusCodes.Status502BadGateway)]
    [InlineData(System.Net.HttpStatusCode.TooManyRequests, StatusCodes.Status503ServiceUnavailable)]
    [InlineData(System.Net.HttpStatusCode.InternalServerError, StatusCodes.Status502BadGateway)]
    public async Task Upstream_chatbot_statuses_are_mapped_without_impersonating_student_authentication(System.Net.HttpStatusCode upstreamStatus, int expectedStatus)
    {
        var context = NewContext();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        bool handled = await handler.TryHandleAsync(context, new StudyChatRemoteException(upstreamStatus, "upstream detail"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.DoesNotContain("upstream detail", await ReadBodyAsync(context));
    }

    [Fact]
    public async Task Internal_chatbot_timeout_returns_safe_gateway_timeout_problem()
    {
        var context = new DefaultHttpContext();
        using ServiceProvider services = new ServiceCollection().AddLogging().BuildServiceProvider();
        context.RequestServices = services;
        context.Response.Body = new MemoryStream();
        var middleware = new StudyChatbotTimeoutMiddleware(_ =>
            throw new StudyChatTimeoutException(), NullLogger<StudyChatbotTimeoutMiddleware>.Instance);

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
            throw new StudyChatTimeoutException(), NullLogger<StudyChatbotTimeoutMiddleware>.Instance);

        await Assert.ThrowsAsync<StudyChatTimeoutException>(() => middleware.InvokeAsync(context));
        Assert.NotEqual(StatusCodes.Status504GatewayTimeout, context.Response.StatusCode);
    }

    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "test-trace-id" };
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var body = new StreamReader(context.Response.Body, leaveOpen: true);
        return await body.ReadToEndAsync();
    }
}

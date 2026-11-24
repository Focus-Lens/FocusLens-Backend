using FocusLens.Infrastructure.StudyChat;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace FocusLens.API.Infrastructure;

/// <summary>Converts known chat failures into the public API's ProblemDetails contract.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        // A disconnected caller and an already-written response must retain the normal ASP.NET
        // behavior; attempting to write a ProblemDetails document in either case is unsafe.
        if (context.RequestAborted.IsCancellationRequested || context.Response.HasStarted)
            return false;

        (int statusCode, string title, string detail)? problem = exception switch
        {
            StudyChatException chat => (chat.StatusCode, "Study chat request failed", chat.Message),
            StudyChatTimeoutException => (StatusCodes.Status504GatewayTimeout, "Study chatbot timed out", "The study chatbot did not respond in time. Please try again."),
            StudyChatConfigurationException => (StatusCodes.Status503ServiceUnavailable, "Study chatbot unavailable", "The study chatbot is not currently configured. Please try again later."),
            StudyChatRemoteException remote => MapRemote(remote.StatusCode),
            RedisException => (StatusCodes.Status503ServiceUnavailable, "Study chat temporarily unavailable", "Temporary chat storage is unavailable. Please try again later."),
            _ => null
        };

        if (problem is null)
            return false;

        logger.LogError(exception,
            "Study chat failure. TraceId: {TraceId}; StatusCode: {StatusCode}; ExceptionType: {ExceptionType}",
            context.TraceIdentifier, problem.Value.statusCode, exception.GetType().Name);

        ProblemDetails response = new()
        {
            Status = problem.Value.statusCode,
            Title = problem.Value.title,
            Detail = problem.Value.detail,
            Instance = context.Request.Path
        };
        response.Extensions["traceId"] = context.TraceIdentifier;
        await Results.Problem(response.Detail, response.Instance, response.Status, response.Title, extensions: response.Extensions)
            .ExecuteAsync(context);
        return true;
    }

    private static (int statusCode, string title, string detail) MapRemote(System.Net.HttpStatusCode statusCode) =>
        statusCode == System.Net.HttpStatusCode.TooManyRequests
            ? (StatusCodes.Status503ServiceUnavailable, "Study chatbot temporarily unavailable", "The study chatbot is busy. Please try again later.")
            : (StatusCodes.Status502BadGateway, "Study chatbot unavailable", "The study chatbot could not process the request. Please try again later.");
}

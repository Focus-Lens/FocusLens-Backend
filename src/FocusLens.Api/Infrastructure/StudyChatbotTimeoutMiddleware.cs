using FocusLens.Infrastructure.StudyChat;

namespace FocusLens.API.Infrastructure;

public sealed class StudyChatbotTimeoutMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (StudyChatTimeoutException) when (!context.RequestAborted.IsCancellationRequested)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            await Results.Problem(
                statusCode: StatusCodes.Status504GatewayTimeout,
                title: "Study chatbot timed out",
                detail: "The study chatbot did not respond in time. Please try again.")
                .ExecuteAsync(context);
        }
    }
}

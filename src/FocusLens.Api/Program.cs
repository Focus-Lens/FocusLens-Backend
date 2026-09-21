using FocusLens.Application;
using FocusLens.Infrastructure;
using FocusLens.Infrastructure.Data;
using Scalar.AspNetCore;
using Serilog;

namespace FocusLens.API;

public class Program
{
    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // API services
        builder.Services.AddApiServices(builder.Configuration);

        builder.Services.AddHttpClient();

        // OpenAPI
        builder.Services.AddOpenApi();

        // Application + Infrastructure
        builder.Services.AddApplication().AddInfrastructure(builder.Configuration);

        // Serilog
        builder.Host.UseSerilog(
            (context, configuration) => configuration.ReadFrom.Configuration(context.Configuration)
        );
        builder.Services.AddHttpClient();

        // CORS
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(
                "AllowAll",
                policy =>
                {
                    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                }
            );
        });

        WebApplication app = builder.Build();

        // Apply EF Core migrations and seed the required identity roles outside
        // the integration-test host, which seeds its own isolated database.
        if (!app.Environment.IsEnvironment("Testing"))
        {
            using IServiceScope scope = app.Services.CreateScope();
            ApplicationDbContextInitialiser initialiser =
                scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();

            await initialiser.SeedAsync();
        }

        // Development-only API documentation
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();

            app.MapScalarApiReference();
        }

        // Middleware
        app.UseHttpsRedirection();

        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set(
                    "RequestPath",
                    RedactInvitationToken(httpContext.Request.Path)
                );
            };
        });

        app.UseCors("AllowAll");

        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();

        // Controllers
        app.MapControllers();

        // Simple health check
        app.MapGet(
            "/health",
            () => Results.Ok(new { status = "Healthy", application = "FocusLens.Api" })
        );

        app.Run();
    }

    private static string RedactInvitationToken(PathString requestPath)
    {
        string[] segments = (requestPath.Value ?? string.Empty).Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries
        );

        for (int index = 0; index < segments.Length - 1; index++)
        {
            if (
                segments[index]
                    .Equals("child-setup-invitations", StringComparison.OrdinalIgnoreCase)
            )
            {
                segments[index + 1] = "[redacted]";
                break;
            }
        }

        return "/" + string.Join('/', segments);
    }
}

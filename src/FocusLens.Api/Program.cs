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
        var builder = WebApplication.CreateBuilder(args);

        // API services
        builder.Services.AddApiServices(builder.Configuration);

        // OpenAPI
        builder.Services.AddOpenApi();

        // Application + Infrastructure
        builder.Services
            .AddApplication()
            .AddInfrastructure(builder.Configuration);

        // Serilog
        builder.Host.UseSerilog(
            (context, configuration) =>
                configuration.ReadFrom.Configuration(context.Configuration)
        );

        // CORS
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(
                "AllowAll",
                policy =>
                {
                    policy
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                }
            );
        });

        var app = builder.Build();

        // Development
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();

            app.MapScalarApiReference();

            // Seed database
            using IServiceScope scope = app.Services.CreateScope();

            ApplicationDbContextInitialiser initialiser =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContextInitialiser>();

            await initialiser.SeedAsync();
        }

        // Middleware
        app.UseHttpsRedirection();

        app.UseSerilogRequestLogging();

        app.UseCors("AllowAll");

        app.UseAuthentication();
        app.UseAuthorization();

        // Controllers
        app.MapControllers();

        // Simple health check
        app.MapGet("/health", () => Results.Ok(new
        {
            status = "Healthy",
            application = "FocusLens.Api"
        }));

        app.Run();
    }
}

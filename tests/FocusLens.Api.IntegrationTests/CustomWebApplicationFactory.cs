using FocusLens.API;
using FocusLens.API.Infrastructure;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Contracts.BehavioralIntelligence;
using FocusLens.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using FocusLens.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FocusLens.Api.IntegrationTests;

public sealed class CustomWebApplicationFactory
    : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"FocusLensTests-{Guid.NewGuid()}";
    private readonly TimeProvider? _timeProvider;

    public CustomWebApplicationFactory(DateTimeOffset? utcNow = null)
    {
        _timeProvider = utcNow is null ? null : new FixedTimeProvider(utcNow.Value);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Secret", "FocusLens.IntegrationTests.Jwt.Secret.2026.1234567890abcdef");
        builder.UseSetting("Jwt:Issuer", "FocusLens.Api");
        builder.UseSetting("Jwt:Audience", "FocusLens.Client");
        builder.UseSetting("FocusLensAI:BaseUrl", "http://localhost/");
        builder.UseSetting("BehavioralIntelligence:BaseUrl", "http://localhost/");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<IEmailSender>();

            if (_timeProvider is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(_timeProvider);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName)
                    .ConfigureWarnings(warnings =>
                        warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            services.AddScoped<IEmailSender, LoggingEmailSender>();

            // Keep API integration tests deterministic and independent of a live Python service.
            services.RemoveAll<IBehavioralIntelligenceClient>();
            services.AddSingleton<IBehavioralIntelligenceClient, TestBehavioralIntelligenceClient>();

            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            return Task.CompletedTask;
                        }
                    };
                });
        });
    }

    public async Task SeedAsync(Func<ApplicationDbContext, Task> seed)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.EnsureCreatedAsync();
        await seed(dbContext);
        await dbContext.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TestBehavioralIntelligenceClient : IBehavioralIntelligenceClient
    {
        public Task<BehaviorWindowResponse> AnalyzeWindowAsync(
            BehaviorWindowRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            BehaviorSectionResult[] sections = request.Sections
                .Select(section => new BehaviorSectionResult(
                    section.SectionId,
                    section.ConceptId,
                    "NORMAL_FOCUSED",
                    0.9,
                    75,
                    "CONTINUE",
                    new Dictionary<string, object?>(),
                    section.MicroChallenges.Count > 0))
                .ToArray();

            BehaviorWindowResponse response = new(
                request.SessionId,
                request.WindowIndex,
                75,
                100,
                "STABLE",
                "NORMAL_FOCUSED",
                "CONTINUE",
                "CONTINUE",
                false,
                "STABLE",
                request.IsFinal,
                sections.Length,
                sections)
            {
                WindowActiveTimeSeconds = Math.Max(
                    0,
                    request.Sections.Sum(section => section.TimeSpentSeconds))
            };

            return Task.FromResult(response);
        }
    }
}

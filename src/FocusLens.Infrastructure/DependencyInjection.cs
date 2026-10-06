using System.Text;
using System.Security.Claims;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using FocusLens.Domain.StudySessions;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Infrastructure.AI;
using FocusLens.Infrastructure.Authentication;
using FocusLens.Infrastructure.Data;
using FocusLens.Infrastructure.Data.Interceptors;
using FocusLens.Infrastructure.Identity;
using FocusLens.Infrastructure.Identity.Seed;
using FocusLens.Infrastructure.Notifications;
using FocusLens.Infrastructure.ProfileImages;
using FocusLens.Infrastructure.StudySessions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FocusLens.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        string connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found."
            );

        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ApplicationDbContextInitialiser>();
        services.AddScoped<RoleSeeder>();
        services.AddScoped<UserSeeder>();
        services.AddScoped<UserRoleSeeder>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<ITokenProvider, TokenProvider>();
        services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.Configure<StudyMaterialStorageOptions>(
            configuration.GetRequiredSection(StudyMaterialStorageOptions.SectionName));
        services.Configure<StudySessionImageStorageOptions>(
            configuration.GetSection(StudySessionImageStorageOptions.SectionName));
        services.Configure<ProfileImageStorageOptions>(
            configuration.GetSection(ProfileImageStorageOptions.SectionName));
        services.Configure<StudySessionImageUploadOptions>(
            configuration.GetSection(StudySessionImageUploadOptions.SectionName));
          services.AddOptions<FocusLensAiOptions>()
              .Bind(configuration.GetRequiredSection(FocusLensAiOptions.SectionName))
              .Validate(
                  options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
                  "FocusLensAI:BaseUrl must be a valid absolute URI.")
              .ValidateOnStart();

          services.AddHttpClient<IFocusLensAiClient, FocusLensAiClient>(
              (serviceProvider, client) =>
              {
                  FocusLensAiOptions options =
                      serviceProvider.GetRequiredService<IOptions<FocusLensAiOptions>>().Value;

                  client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
              });

        services.AddOptions<BehavioralIntelligenceOptions>()
            .Bind(configuration.GetRequiredSection(BehavioralIntelligenceOptions.SectionName))
            .Validate(
                options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
                "BehavioralIntelligence:BaseUrl must be a valid absolute URI.")
            .ValidateOnStart();

        services.AddHttpClient<IBehavioralIntelligenceClient, BehavioralIntelligenceClient>(
            (serviceProvider, client) =>
            {
                BehavioralIntelligenceOptions options =
                    serviceProvider.GetRequiredService<IOptions<BehavioralIntelligenceOptions>>().Value;

                client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
            });

        services.AddScoped<IStudyMaterialFileStore, LocalStudyMaterialFileStore>();
        services.AddScoped<IStudySessionImageFileStore, LocalStudySessionImageFileStore>();
        services.AddScoped<IProfileImageFileStore, LocalProfileImageFileStore>();
        services.AddScoped<IStudyMaterialPdfProcessor, PdfSharpStudyMaterialPdfProcessor>();
        services.AddSingleton<IPushNotificationProvider, NoopPushNotificationProvider>();
        services.AddHostedService<ScheduledNotificationWorker>();
        services.AddHostedService<PushNotificationDispatcher>();
        services.AddHostedService<AccountDeletionWorker>();

        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            {
                options.AddInterceptors(serviceProvider.GetServices<ISaveChangesInterceptor>());
                options.UseSqlServer(connectionString);
            }
        );

        services
            .AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;

                options.SignIn.RequireConfirmedEmail = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<JwtOptions>(
            configuration.GetRequiredSection(JwtOptions.SectionName)
        );
        services.Configure<GoogleAuthOptions>(
            configuration.GetRequiredSection(GoogleAuthOptions.SectionName)
        );
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

        JwtOptions jwtOptions =
            configuration.GetRequiredSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException(
                $"Configuration section '{JwtOptions.SectionName}' was not found."
            );

        ValidateOptionsResult validationResult = new JwtOptionsValidator().Validate(
            null,
            jwtOptions
        );

        if (validationResult.Failed)
        {
            throw new OptionsValidationException(
                nameof(JwtOptions),
                typeof(JwtOptions),
                validationResult.Failures
            );
        }

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = true;
                options.SaveToken = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.Secret)
                    ),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        string? userIdValue = context.Principal?.FindFirstValue(
                            ClaimTypes.NameIdentifier);
                        if (!Guid.TryParse(userIdValue, out Guid userId))
                        {
                            context.Fail("The token does not identify a valid user.");
                            return;
                        }

                        ApplicationDbContext dbContext =
                            context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                        ApplicationUser? user = await dbContext.Users.FindAsync(
                            [userId],
                            context.HttpContext.RequestAborted);

                        if (user is null || user.IsDisabled)
                        {
                            context.Fail("The user account is disabled or unavailable.");
                        }
                    }
                };
            });

        services.AddAuthorization();

        services.AddHostedService<StudySessionBehaviorAnalysisWorker>();

        return services;
    }
}

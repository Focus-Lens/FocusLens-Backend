using FocusLens.API.Infrastructure;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Infrastructure.Authentication;
using FocusLens.Settings;

namespace FocusLens.API;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddControllers();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IEmailVerificationCodeStore, EmailVerificationCodeStore>();
        services.AddScoped<IEmailSender, LoggingEmailSender>();
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddSingleton<EmailTemplateRenderer>();
        services.Configure<GoogleAuthOptions>(
            configuration.GetSection(GoogleAuthOptions.SectionName));
        services.AddOptions<MailSettings>()
            .Bind(configuration.GetRequiredSection(MailSettings.SectionName));

        return services;
    }
}

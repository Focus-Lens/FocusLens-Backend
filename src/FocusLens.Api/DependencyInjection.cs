using System.Text.Json.Serialization;
using FocusLens.API.Infrastructure;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Features.Identity.Options;
using FocusLens.Infrastructure.Authentication;
using FocusLens.Settings;

namespace FocusLens.API;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter());
            });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IEmailVerificationCodeStore, EmailVerificationCodeStore>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IInvitationUrlBuilder, InvitationUrlBuilder>();
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddSingleton<EmailTemplateRenderer>();
        services.Configure<GoogleAuthOptions>(
            configuration.GetSection(GoogleAuthOptions.SectionName));
        services.AddOptions<MailSettings>()
            .Bind(configuration.GetRequiredSection(MailSettings.SectionName));

        services.AddOptions<InvitationSettings>()
            .Bind(configuration.GetRequiredSection(InvitationSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegistrationOptions registrationOptions = configuration
                                                      .GetRequiredSection(RegistrationOptions.SectionName)
                                                      .Get<RegistrationOptions>()
                                                  ?? throw new InvalidOperationException(
                                                      $"Configuration section {RegistrationOptions.SectionName} was not found.");

        services.AddSingleton(registrationOptions);

        return services;
    }
}
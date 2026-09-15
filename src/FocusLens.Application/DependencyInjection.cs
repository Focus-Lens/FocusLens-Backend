using FluentValidation;
using FocusLens.Application.Common.Behaviours;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.StudySessions;
using FocusLens.Application.Notifications;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FocusLens.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(config =>
            config.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<Reports.ReportSessionMetricsCalculator>();
        services.AddScoped<BehavioralIntelligence.BehaviorWindowBuilder>();
        services.AddScoped<BehavioralIntelligence.BehaviorWindowTimelineCalculator>();

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddScoped<INotificationWriter, NotificationWriter>();
        services.AddScoped<IStudySessionFinalBehaviorAnalysisService, StudySessionFinalBehaviorAnalysisService>();

        return services;
    }
}

using FluentValidation;
using FocusLens.Application.BehavioralIntelligence;
using FocusLens.Application.Common.Behaviours;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Common.Services;
using FocusLens.Application.Notifications;
using FocusLens.Application.Reports;
using FocusLens.Application.StudySessions;
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

        services.AddScoped<ReportSessionMetricsCalculator>();
        services.AddScoped<BehaviorWindowBuilder>();
        services.AddScoped<BehaviorWindowTimelineCalculator>();

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddScoped<INotificationWriter, NotificationWriter>();
        services.AddScoped<IStudySessionFinalBehaviorAnalysisService, StudySessionFinalBehaviorAnalysisService>();
        services.AddScoped<IStudentLocalTime, StudentLocalTime>();

        return services;
    }
}
using FluentValidation;
using FocusLens.Application.Common.Behaviours;
using FocusLens.Application.Terms;
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

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddSingleton<ITermsProvider, CurrentTermsProvider>();

        return services;
    }
}
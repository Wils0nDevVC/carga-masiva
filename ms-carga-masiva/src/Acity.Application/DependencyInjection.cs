using Acity.Application.Behaviors;
using Acity.Application.Cargas.Procesamiento;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Acity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(assembly);
        services.AddScoped<ProcesadorCarga>();

        return services;
    }
}

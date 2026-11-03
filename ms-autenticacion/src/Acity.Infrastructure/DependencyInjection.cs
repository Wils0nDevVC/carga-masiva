using Acity.Application.Interfaces.Repositories;
using Acity.Application.Interfaces.Services;
using Acity.Infrastructure.Persistence;
using Acity.Infrastructure.Repositories;
using Acity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // La cadena de conexión llega por variable de entorno (ConnectionStrings__Default); no hay valores en archivos.
        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(configuration.GetConnectionString("Default")));

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAutenticacionRepository, AutenticacionRepository>();

        // JWT (Feature 008): sin configuración válida el servicio no arranca; el mensaje no incluye valores.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.Seccion))
            .Validate(o => o.EsValida(),
                $"Configuración Jwt inválida: Clave (>= {JwtOptions.LongitudMinimaClaveBytes} bytes), Emisor, Audiencia y vigencias (> 0) son obligatorios.")
            .ValidateOnStart();
        services.AddSingleton<IEmisorTokens, EmisorTokens>();
        services.AddSingleton<IHashContrasena, HashContrasena>();

        return services;
    }
}

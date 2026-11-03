using Acity.Application.Interfaces.Repositories;
using Acity.Application.Interfaces.Services;
using Acity.Infrastructure.Excel;
using Acity.Infrastructure.Messaging;
using Acity.Infrastructure.Persistence;
using Acity.Infrastructure.Repositories;
using Acity.Infrastructure.Resilience;
using Acity.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // La cadena de conexión llega por variable de entorno (ConnectionStrings__Default); no hay valores en archivos.
        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(configuration.GetConnectionString("Default")));

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddOptions<SeaweedFsOptions>()
            .Bind(configuration.GetSection(SeaweedFsOptions.Seccion))
            .Validate(o => o.EsValida(), "Configuración SeaweedFs inválida: FilerUrl debe ser una URI absoluta.")
            .ValidateOnStart();
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.Seccion))
            .Validate(o => o.EsValida(), "Configuración RabbitMq inválida: Host, Puerto, Usuario y Clave son obligatorios.")
            .ValidateOnStart();

        // Pipelines de Polly por operación (research D-10).
        services.AddKeyedSingleton(PipelinesResiliencia.SeaweedFs, (sp, _) =>
            PipelinesResiliencia.CrearSeaweedFs(sp.GetRequiredService<ILoggerFactory>().CreateLogger("Resiliencia.SeaweedFs")));
        services.AddKeyedSingleton(PipelinesResiliencia.RabbitMq, (sp, _) =>
            PipelinesResiliencia.CrearRabbitMq(sp.GetRequiredService<ILoggerFactory>().CreateLogger("Resiliencia.RabbitMq")));
        services.AddKeyedSingleton(PipelinesResiliencia.Procedimiento, (sp, _) =>
            PipelinesResiliencia.CrearProcedimiento(sp.GetRequiredService<ILoggerFactory>().CreateLogger("Resiliencia.Procedimiento")));

        services.AddHttpClient<IAlmacenamientoArchivos, SeaweedFsAlmacenamiento>();
        services.AddSingleton<IPublicadorCargas, RabbitMqPublicadorCargas>();
        services.AddScoped<IEstadoCargaService, EstadoCargaService>();

        // Consultas de seguimiento (Feature 010): solo lectura.
        services.AddScoped<ICargasConsulta, CargasConsulta>();
        services.AddSingleton<ILectorContenidoExcel, LectorContenidoExcelClosedXml>();

        return services;
    }
}

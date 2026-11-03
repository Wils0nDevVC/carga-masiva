using Acity.Application.Interfaces.Repositories;
using Acity.Application.Interfaces.Services;
using Acity.Infrastructure.Excel;
using Acity.Infrastructure.Locking;
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

        // Pipelines de Polly por operación (research D-12).
        services.AddKeyedSingleton(PipelinesResiliencia.Descarga, (sp, _) =>
            PipelinesResiliencia.CrearDescarga(Logger(sp, "Resiliencia.Descarga")));
        services.AddKeyedSingleton(PipelinesResiliencia.Sql, (sp, _) =>
            PipelinesResiliencia.CrearSql(Logger(sp, "Resiliencia.Sql")));
        services.AddKeyedSingleton(PipelinesResiliencia.TransaccionGrupo, (sp, _) =>
            PipelinesResiliencia.CrearTransaccionGrupo(Logger(sp, "Resiliencia.TransaccionGrupo")));
        services.AddKeyedSingleton(PipelinesResiliencia.Notificacion, (sp, _) =>
            PipelinesResiliencia.CrearNotificacion(Logger(sp, "Resiliencia.Notificacion")));

        services.AddScoped<IProcedimientosCarga, ProcedimientosCarga>();
        services.AddScoped<IRepositorioProcesamiento, RepositorioProcesamiento>();
        services.AddHttpClient<IAlmacenamientoArchivos, SeaweedFsAlmacenamiento>();
        services.AddSingleton<RabbitMqConexion>();
        services.AddSingleton<IPublicadorNotificaciones, PublicadorNotificaciones>();
        services.AddSingleton<ICerrojoConsumidor, CerrojoConsumidorSql>();
        services.AddSingleton<ILectorExcel, LectorExcelClosedXml>();
        services.AddSingleton<ConsumidorRabbitMq>();

        return services;
    }

    private static ILogger Logger(IServiceProvider sp, string categoria)
        => sp.GetRequiredService<ILoggerFactory>().CreateLogger(categoria);
}

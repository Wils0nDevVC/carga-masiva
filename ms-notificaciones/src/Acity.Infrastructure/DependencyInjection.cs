using Acity.Application.Interfaces.Repositories;
using Acity.Application.Interfaces.Services;
using Acity.Infrastructure.Mail;
using Acity.Infrastructure.Messaging;
using Acity.Infrastructure.Persistence;
using Acity.Infrastructure.Repositories;
using Acity.Infrastructure.Resilience;
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

        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.Seccion))
            .Validate(o => o.EsValida(), "Configuración Smtp inválida: Host, Puerto, Seguridad (None, StartTls, SslOnConnect, Auto), Remitente válido y Clave si hay Usuario.")
            .ValidateOnStart();
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.Seccion))
            .Validate(o => o.EsValida(), "Configuración RabbitMq inválida: Host, Puerto, Usuario y Clave son obligatorios.")
            .ValidateOnStart();

        // Pipelines de Polly (research D-05): singletons, su circuito conserva el estado entre mensajes.
        services.AddKeyedSingleton(PipelinesResiliencia.Smtp, (sp, _) =>
            PipelinesResiliencia.CrearSmtp(sp.GetRequiredService<ILoggerFactory>().CreateLogger("Resiliencia.Smtp")));
        services.AddKeyedSingleton(PipelinesResiliencia.Sql, (sp, _) =>
            PipelinesResiliencia.CrearSql(sp.GetRequiredService<ILoggerFactory>().CreateLogger("Resiliencia.Sql")));

        services.AddScoped<IProcedimientosCarga, ProcedimientosCarga>();
        services.AddScoped<ICargasConsulta, CargasConsulta>();
        services.AddSingleton<IEnvioCorreo, EnvioCorreoMailKit>();
        services.AddSingleton<RabbitMqConexion>();
        services.AddSingleton<ConsumidorRabbitMq>();

        return services;
    }
}

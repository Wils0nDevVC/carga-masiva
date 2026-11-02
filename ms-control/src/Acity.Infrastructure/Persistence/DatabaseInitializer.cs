using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acity.Infrastructure.Persistence;

/// <summary>
/// Inicialización del esquema de la base compartida. Solo ms-control la usa: es el único propietario
/// de las migraciones. Se invoca entre Build() y Run(), por lo que el servicio no escucha hasta terminar.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider services, IConfiguration configuration, ILogger logger, CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Falta la cadena de conexión 'ConnectionStrings:Default' (variable de entorno ConnectionStrings__Default).");
        }

        var attempts = int.TryParse(configuration["Startup:SqlWaitAttempts"], out var a) && a > 0 ? a : 30;
        var delaySeconds = int.TryParse(configuration["Startup:SqlWaitSeconds"], out var d) && d >= 0 ? d : 2;

        await WaitForSqlServerAsync(connectionString, attempts, delaySeconds, logger, cancellationToken);

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await EnsureNotLegacyDatabaseAsync(db, logger, cancellationToken);

        logger.LogInformation("Aplicando migraciones de EF Core...");
        await db.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Migraciones aplicadas; la inicialización del esquema terminó.");
    }

    // Se usa la base 'master' de la misma instancia: la base objetivo puede no existir todavía.
    private static async Task WaitForSqlServerAsync(
        string connectionString, int attempts, int delaySeconds, ILogger logger, CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master", ConnectTimeout = 5 };

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                await using var connection = new SqlConnection(builder.ConnectionString);
                await connection.OpenAsync(cancellationToken);
                logger.LogInformation("SQL Server disponible (intento {Attempt}/{Attempts}).", attempt, attempts);
                return;
            }
            catch (SqlException ex)
            {
                // Sin la cadena de conexión: solo el número de error.
                logger.LogWarning("SQL Server no disponible (intento {Attempt}/{Attempts}, error {Number}).", attempt, attempts, ex.Number);
                if (attempt < attempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                }
            }
        }

        throw new InvalidOperationException($"SQL Server no estuvo disponible tras {attempts} intentos.");
    }

    // FR-034: no se migra encima de una base creada con los scripts anteriores ni se inventa un historial.
    private static async Task EnsureNotLegacyDatabaseAsync(AppDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            return; // la base no existe: MigrateAsync la crea vacía
        }

        var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken);
        if (applied.Any())
        {
            return;
        }

        var userTables = await db.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.tables WHERE name <> N'__EFMigrationsHistory'")
            .SingleAsync(cancellationToken);

        if (userTables > 0)
        {
            logger.LogCritical("La base de datos tiene {Tables} tabla(s) y ningún historial de migraciones de EF Core.", userTables);
            throw new InvalidOperationException(
                "La base de datos configurada contiene objetos sin historial de migraciones de EF Core (¿creada con los scripts anteriores?). " +
                "No se aplica la migración inicial ni se inventa un historial. Use una base nueva y vacía (DB_NAME).");
        }
    }
}

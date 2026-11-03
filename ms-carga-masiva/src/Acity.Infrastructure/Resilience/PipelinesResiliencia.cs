using System.Net;
using Acity.Application.Interfaces.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using RabbitMQ.Client.Exceptions;

namespace Acity.Infrastructure.Resilience;

/// <summary>
/// Pipelines de Polly por operación según su riesgo de duplicación (research D-12): reintentos limitados, Circuit
/// Breaker y tiempo de espera por intento. Nunca se reintentan una ausencia confirmada del archivo, un archivo
/// inválido, una colisión de código ni un circuito abierto.
/// </summary>
public static class PipelinesResiliencia
{
    public const string Descarga = "descarga";
    public const string Sql = "sql";
    public const string TransaccionGrupo = "transaccion-grupo";
    public const string Notificacion = "notificacion";

    /// <summary>Errores SQL transitorios: deadlock, tiempo de espera y pérdida o indisponibilidad de conexión.</summary>
    private static readonly HashSet<int> ErroresSqlTransitorios = [1205, -2, 53, 10053, 10054, 10060, 40613, 40197, 40501];

    public static bool EsSqlTransitorio(Exception ex) => ex switch
    {
        // Un circuito abierto nunca se reintenta, aunque su excepción interna sea la transitoria que lo abrió.
        BrokenCircuitException => false,
        SqlException sql => ErroresSqlTransitorios.Contains(sql.Number),
        TimeoutException => true,
        _ when ex.InnerException is not null => EsSqlTransitorio(ex.InnerException),
        _ => false
    };

    /// <summary>GET al filer (lectura): 404 (ausencia confirmada) y otros 4xx no se reintentan.</summary>
    public static ResiliencePipeline CrearDescarga(ILogger logger) => Crear(
        logger, Descarga, 3, TimeSpan.FromSeconds(60),
        new PredicateBuilder()
            .Handle<HttpRequestException>(ex => ex.StatusCode is null
                or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
                || (int)ex.StatusCode >= 500)
            .Handle<TimeoutRejectedException>());

    /// <summary>Procedimientos (idempotentes), incremento de intentos y consultas.</summary>
    public static ResiliencePipeline CrearSql(ILogger logger) => Crear(
        logger, Sql, 3, TimeSpan.FromSeconds(30),
        new PredicateBuilder().Handle<Exception>(EsSqlTransitorio).Handle<TimeoutRejectedException>());

    /// <summary>Transacción de un periodo: cada intento comprueba antes si el periodo ya quedó Completado.</summary>
    public static ResiliencePipeline CrearTransaccionGrupo(ILogger logger) => Crear(
        logger, TransaccionGrupo, 3, TimeSpan.FromSeconds(120),
        new PredicateBuilder().Handle<Exception>(EsSqlTransitorio).Handle<TimeoutRejectedException>());

    /// <summary>Publicación con confirmación; un reintento tras una confirmación perdida puede duplicar el mensaje.</summary>
    public static ResiliencePipeline CrearNotificacion(ILogger logger) => Crear(
        logger, Notificacion, 2, TimeSpan.FromSeconds(5),
        new PredicateBuilder()
            .Handle<BrokerUnreachableException>()
            .Handle<AlreadyClosedException>()
            .Handle<OperationInterruptedException>()
            .Handle<PublishException>(ex => !ex.IsReturn)
            .Handle<TimeoutRejectedException>());

    private static ResiliencePipeline Crear(
        ILogger logger, string operacion, int reintentos, TimeSpan timeout, PredicateBuilder<object> reintentables)
    {
        // Orden: reintento (exterior) → circuit breaker → tiempo de espera por intento (interior). Ambos predicados
        // incluyen TimeoutRejectedException porque el tiempo de espera es la estrategia interior. Cada pipeline es un
        // singleton: su circuito conserva el estado entre ejecuciones y cargas (no lo comparte con otro pipeline).
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = reintentos,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
                UseJitter = true,
                ShouldHandle = reintentables,
                OnRetry = args =>
                {
                    logger.LogWarning("Reintento {Intento} de {Operacion} tras {TipoError}",
                        args.AttemptNumber + 1, operacion, args.Outcome.Exception?.GetType().Name);
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = reintentables,
                OnOpened = args =>
                {
                    logger.LogError("Circuito de {Operacion} abierto durante {Duracion}", operacion, args.BreakDuration);
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    logger.LogInformation("Circuito de {Operacion} cerrado", operacion);
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(timeout)
            .Build();
    }

    /// <summary>
    /// Ejecuta en el pipeline y traduce el rechazo por circuito abierto (la operación no se ejecutó) a
    /// <see cref="DependenciaNoDisponibleException"/>, distinto del agotamiento de reintentos (decisión H3).
    /// </summary>
    public static async ValueTask<T> EjecutarAsync<T>(
        this ResiliencePipeline pipeline, string operacion, Func<CancellationToken, ValueTask<T>> accion, CancellationToken cancellationToken)
    {
        try
        {
            return await pipeline.ExecuteAsync(accion, cancellationToken);
        }
        catch (BrokenCircuitException ex)
        {
            throw new DependenciaNoDisponibleException(operacion, ex.RetryAfter, ex);
        }
    }
}

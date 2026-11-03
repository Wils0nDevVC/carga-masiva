using System.Net;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using RabbitMQ.Client.Exceptions;

namespace Acity.Infrastructure.Resilience;

/// <summary>
/// Pipelines de Polly por operación según su riesgo de duplicación (research D-10): reintentos limitados, Circuit
/// Breaker y tiempo de espera por intento. Nunca se reintentan validación, autenticación, autorización ni un circuito
/// abierto. El registro de la carga, la incidencia y la lectura del usuario no usan pipeline (0 reintentos).
/// </summary>
public static class PipelinesResiliencia
{
    public const string SeaweedFs = "seaweedfs";
    public const string RabbitMq = "rabbitmq";
    public const string Procedimiento = "procedimiento-estado";

    /// <summary>Errores SQL transitorios: deadlock, tiempo de espera y pérdida o indisponibilidad de conexión.</summary>
    private static readonly HashSet<int> ErroresSqlTransitorios = [1205, -2, 53, 10053, 10054, 10060, 40613, 40197, 40501];

    /// <summary>PUT a una ruta fija (crear o sobrescribir): reintentar no duplica archivos.</summary>
    public static ResiliencePipeline CrearSeaweedFs(ILogger logger) => Crear(
        logger, SeaweedFs, reintentos: 3, timeout: TimeSpan.FromSeconds(30),
        new PredicateBuilder()
            .Handle<HttpRequestException>(ex => ex.StatusCode is null
                or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
                || (int)ex.StatusCode >= 500)
            .Handle<TimeoutRejectedException>());

    /// <summary>
    /// Publicación con confirmación. Un reintento tras una confirmación perdida puede duplicar el mensaje; se tolera
    /// porque el consumidor descarta cargas fuera de Pendiente. Un mensaje no enrutable (return) no se reintenta.
    /// </summary>
    public static ResiliencePipeline CrearRabbitMq(ILogger logger) => Crear(
        logger, RabbitMq, reintentos: 2, timeout: TimeSpan.FromSeconds(5),
        new PredicateBuilder()
            .Handle<BrokerUnreachableException>()
            .Handle<AlreadyClosedException>()
            .Handle<OperationInterruptedException>()
            .Handle<PublishException>(ex => !ex.IsReturn)
            .Handle<TimeoutRejectedException>());

    /// <summary>usp_CambiarEstadoCarga con origen esperado: idempotente, reintentar no duplica la transición.</summary>
    public static ResiliencePipeline CrearProcedimiento(ILogger logger) => Crear(
        logger, Procedimiento, reintentos: 3, timeout: TimeSpan.FromSeconds(15),
        new PredicateBuilder()
            .Handle<SqlException>(ex => ErroresSqlTransitorios.Contains(ex.Number))
            .Handle<TimeoutException>()
            .Handle<TimeoutRejectedException>());

    private static ResiliencePipeline Crear(ILogger logger, string operacion, int reintentos, TimeSpan timeout, PredicateBuilder<object> reintentables)
        => new ResiliencePipelineBuilder()
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
                }
            })
            .AddTimeout(timeout)
            .Build();
}

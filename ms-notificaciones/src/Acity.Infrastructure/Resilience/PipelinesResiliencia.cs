using Acity.Application.Interfaces.Services;
using MailKit;
using MailKit.Net.Smtp;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Acity.Infrastructure.Resilience;

/// <summary>
/// Pipelines de Polly por operación (research D-05): reintento → circuit breaker propio → tiempo de espera por intento.
/// Cada pipeline es un singleton: su circuito conserva el estado entre mensajes. Un circuito abierto nunca se reintenta.
/// </summary>
public static class PipelinesResiliencia
{
    public const string Smtp = "smtp";
    public const string Sql = "sql";

    private static readonly HashSet<int> ErroresSqlTransitorios = [1205, -2, 53, 10053, 10054, 10060, 40613, 40197, 40501];

    /// <summary>Errores SMTP transitorios: conexión y respuestas 4xx del servidor.</summary>
    public static bool EsSmtpTransitorio(Exception ex) => ex switch
    {
        BrokenCircuitException => false,
        SmtpCommandException cmd => (int)cmd.StatusCode is >= 400 and < 500,
        SmtpProtocolException or ServiceNotConnectedException or IOException or System.Net.Sockets.SocketException => true,
        TimeoutRejectedException => true,
        _ => false
    };

    public static bool EsSqlTransitorio(Exception ex) => ex switch
    {
        BrokenCircuitException => false,
        SqlException sql => ErroresSqlTransitorios.Contains(sql.Number),
        TimeoutException or TimeoutRejectedException => true,
        _ when ex.InnerException is not null => EsSqlTransitorio(ex.InnerException),
        _ => false
    };

    public static ResiliencePipeline CrearSmtp(ILogger logger)
        => Crear(logger, Smtp, TimeSpan.FromSeconds(30), new PredicateBuilder().Handle<Exception>(EsSmtpTransitorio));

    public static ResiliencePipeline CrearSql(ILogger logger)
        => Crear(logger, Sql, TimeSpan.FromSeconds(30), new PredicateBuilder().Handle<Exception>(EsSqlTransitorio));

    private static ResiliencePipeline Crear(ILogger logger, string operacion, TimeSpan timeout, PredicateBuilder<object> reintentables)
        => new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
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

    /// <summary>Ejecuta en el pipeline y traduce el rechazo por circuito abierto a <see cref="DependenciaNoDisponibleException"/>.</summary>
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

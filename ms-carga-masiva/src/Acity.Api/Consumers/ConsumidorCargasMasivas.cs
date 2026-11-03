using System.Text.Json;
using Acity.Application.Cargas.Commands.ProcesarCarga;
using Acity.Application.Cargas.Commands.RecuperarCargas;
using Acity.Application.Interfaces.Services;
using Acity.Infrastructure.Messaging;
using MediatR;

namespace Acity.Api.Consumers;

/// <summary>
/// Consumidor de <c>carga_masiva</c> dentro del host (constitución §3, §9; research D-02): obtener el cerrojo →
/// recuperar → consumir de a un mensaje. Ante la pérdida del cerrojo, un fallo del canal o una excepción no
/// controlada, el mensaje en curso no se confirma; tras una espera vuelve a empezar (recuperando antes de consumir).
/// No contiene reglas de negocio.
/// </summary>
public sealed class ConsumidorCargasMasivas(
    IServiceScopeFactory scopes,
    ICerrojoConsumidor cerrojo,
    ConsumidorRabbitMq consumidor,
    ILogger<ConsumidorCargasMasivas> logger) : BackgroundService
{
    private static readonly TimeSpan EsperaCerrojo = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan EsperaTrasFallo = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan AperturaCircuito = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await cerrojo.IntentarObtenerAsync(stoppingToken))
                {
                    logger.LogInformation("Otra instancia tiene el cerrojo del consumidor: no se consume ni se recupera");
                    await Task.Delay(EsperaCerrojo, stoppingToken);
                    continue;
                }

                await using (var scope = scopes.CreateAsyncScope())
                {
                    var recuperadas = await scope.ServiceProvider.GetRequiredService<IMediator>()
                        .Send(new RecuperarCargasCommand(), stoppingToken);
                    logger.LogInformation("Recuperación terminada: {Cargas} carga(s)", recuperadas);
                }

                await consumidor.ConsumirAsync(ProcesarMensajeAsync, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Circuito abierto: esperar a que pueda cerrarse (RetryAfter, o la apertura de 30 s; nunca menos de la
                // espera general) para no crear bucles inmediatos de reentrega (decisión H3).
                var espera = ex is DependenciaNoDisponibleException circuito
                    ? Max(circuito.RetryAfter ?? AperturaCircuito, EsperaTrasFallo)
                    : EsperaTrasFallo;
                logger.LogError(ex, "El consumidor se detuvo; se reintenta en {Espera}", espera);
                await cerrojo.LiberarAsync();
                try
                {
                    await Task.Delay(espera, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        await cerrojo.LiberarAsync();
    }

    /// <summary>Un mensaje: si no cumple el contrato se descarta (se confirma); si no, se procesa en un ámbito propio.</summary>
    private async Task ProcesarMensajeAsync(ReadOnlyMemory<byte> cuerpo, CancellationToken stoppingToken)
    {
        MensajeCargaMasiva? mensaje;
        try
        {
            mensaje = JsonSerializer.Deserialize<MensajeCargaMasiva>(cuerpo.Span, Json);
        }
        catch (JsonException)
        {
            mensaje = null;
        }

        if (mensaje is not { IdCarga: > 0 })
        {
            logger.LogError("Mensaje descartado: no cumple el contrato de carga_masiva");
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var resultado = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new ProcesarCargaCommand(mensaje.IdCarga, mensaje.RutaArchivo), CancellationToken.None);
        logger.LogInformation("Mensaje de la carga {IdCarga}: {Resultado}", mensaje.IdCarga, resultado);
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    /// <summary>Contrato de entrada (F005): { idCarga, rutaArchivo, usuario }.</summary>
    private sealed record MensajeCargaMasiva(long IdCarga, string? RutaArchivo, string? Usuario);
}

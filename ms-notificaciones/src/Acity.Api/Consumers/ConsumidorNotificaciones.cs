using System.Text.Json;
using Acity.Application.Interfaces.Services;
using Acity.Application.Notificaciones.Commands.NotificarCarga;
using Acity.Infrastructure.Messaging;
using MediatR;

namespace Acity.Api.Consumers;

/// <summary>
/// Consumidor de <c>notificaciones</c> dentro del host (constitución §3, §9; research D-02): de a un mensaje con
/// confirmación manual. Si el procesamiento lanza, el mensaje no se confirma y, tras una espera (la del circuito si está
/// abierto), se vuelve a consumir; sin reentrega inmediata en bucle. No contiene reglas de negocio.
/// </summary>
public sealed class ConsumidorNotificaciones(
    IServiceScopeFactory scopes,
    ConsumidorRabbitMq consumidor,
    ILogger<ConsumidorNotificaciones> logger) : BackgroundService
{
    private static readonly TimeSpan EsperaTrasFallo = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan AperturaCircuito = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await consumidor.ConsumirAsync(ProcesarMensajeAsync, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                var espera = ex is DependenciaNoDisponibleException circuito
                    ? Max(circuito.RetryAfter ?? AperturaCircuito, EsperaTrasFallo)
                    : EsperaTrasFallo;
                logger.LogError(ex, "El consumidor de notificaciones se detuvo; se reintenta en {Espera}", espera);
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
    }

    /// <summary>Un mensaje: si no cumple el contrato se descarta (se confirma); si no, se procesa en un ámbito propio.</summary>
    private async Task ProcesarMensajeAsync(ReadOnlyMemory<byte> cuerpo, CancellationToken stoppingToken)
    {
        MensajeNotificacion? mensaje;
        try
        {
            mensaje = JsonSerializer.Deserialize<MensajeNotificacion>(cuerpo.Span, Json);
        }
        catch (JsonException)
        {
            mensaje = null;
        }

        if (mensaje is not { IdCarga: > 0 } || string.IsNullOrWhiteSpace(mensaje.Usuario) || mensaje.FechaFin is null)
        {
            logger.LogError("Mensaje descartado: no cumple el contrato de notificaciones");
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var resultado = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new NotificarCargaCommand(mensaje.IdCarga, mensaje.Usuario.Trim(), mensaje.FechaFin.Value.UtcDateTime), CancellationToken.None);
        logger.LogInformation("Mensaje de la carga {IdCarga}: {Resultado}", mensaje.IdCarga, resultado);
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    /// <summary>Contrato de entrada (F006): { idCarga, usuario, fechaFin }.</summary>
    private sealed record MensajeNotificacion(long IdCarga, string? Usuario, DateTimeOffset? FechaFin);
}

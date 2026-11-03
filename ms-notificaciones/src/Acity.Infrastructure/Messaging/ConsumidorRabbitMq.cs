using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Acity.Infrastructure.Messaging;

/// <summary>
/// Consume la cola <c>notificaciones</c> de a un mensaje con confirmación manual (contrato <c>consumo-notificaciones.md</c>).
/// Confirma un mensaje solo si el procesamiento termina sin excepción; ante una excepción deja de consumir, cierra el
/// canal sin confirmar (RabbitMQ reentregará el mensaje) y propaga el fallo.
/// </summary>
public sealed class ConsumidorRabbitMq(RabbitMqConexion conexion, ILogger<ConsumidorRabbitMq> logger)
{
    public const string Intercambio = "notificaciones";
    public const string Cola = "notificaciones";
    public const string ClaveEnrutamiento = "notificaciones";

    public async Task ConsumirAsync(Func<ReadOnlyMemory<byte>, CancellationToken, Task> procesar, CancellationToken stoppingToken)
    {
        var conn = await conexion.ObtenerAsync(stoppingToken);
        await using var canal = await conn.CreateChannelAsync(cancellationToken: stoppingToken);

        // Misma topología que declara el publicador (F006).
        await canal.ExchangeDeclareAsync(Intercambio, ExchangeType.Direct, durable: true, autoDelete: false,
            arguments: null, cancellationToken: stoppingToken);
        await canal.QueueDeclareAsync(Cola, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: stoppingToken);
        await canal.QueueBindAsync(Cola, Intercambio, ClaveEnrutamiento, arguments: null, cancellationToken: stoppingToken);
        await canal.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

        var fin = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var detenido = false;
        // Un mensaje en curso: al terminar (por fallo del canal o parada) se espera a que acabe antes de volver, para
        // que nunca haya dos ejecuciones a la vez (la recuperación posterior no debe solaparse con la que sigue viva).
        using var enCurso = new SemaphoreSlim(1, 1);

        canal.ChannelShutdownAsync += (_, args) =>
        {
            fin.TrySetException(new InvalidOperationException($"Canal de consumo cerrado: {args.ReplyText}"));
            return Task.CompletedTask;
        };

        var consumidor = new AsyncEventingBasicConsumer(canal);
        consumidor.ReceivedAsync += async (_, entrega) =>
        {
            await enCurso.WaitAsync(CancellationToken.None);
            try
            {
                if (detenido)
                {
                    return;
                }

                await procesar(entrega.Body, stoppingToken);
                await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false, CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Sin confirmar: el mensaje se reentrega al cerrar el canal.
                detenido = true;
                fin.TrySetException(ex);
            }
            finally
            {
                enCurso.Release();
            }
        };

        await canal.BasicConsumeAsync(Cola, autoAck: false, consumidor, stoppingToken);
        logger.LogInformation("Consumiendo la cola {Cola}", Cola);

        try
        {
            await using (stoppingToken.Register(() => fin.TrySetResult()))
            {
                await fin.Task;
            }
        }
        finally
        {
            detenido = true;
            await enCurso.WaitAsync(CancellationToken.None);
        }
    }
}

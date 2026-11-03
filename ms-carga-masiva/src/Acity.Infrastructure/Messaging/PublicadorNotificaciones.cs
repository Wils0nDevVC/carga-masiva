using System.Text.Json;
using Acity.Application.Interfaces.Services;
using Acity.Infrastructure.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using RabbitMQ.Client;

namespace Acity.Infrastructure.Messaging;

/// <summary>
/// Publica en el intercambio directo <c>notificaciones</c> con confirmación del intermediario (contrato
/// <c>mensaje-notificaciones.md</c>): completa cuando el broker confirma y lanza ante un rechazo o un no enrutable.
/// </summary>
public sealed class PublicadorNotificaciones(
    RabbitMqConexion conexion,
    [FromKeyedServices(PipelinesResiliencia.Notificacion)] ResiliencePipeline pipeline) : IPublicadorNotificaciones
{
    public const string Intercambio = "notificaciones";
    public const string Cola = "notificaciones";
    public const string ClaveEnrutamiento = "notificaciones";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly CreateChannelOptions CanalConConfirmaciones =
        new(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);

    public Task PublicarAsync(MensajeNotificacion mensaje, CancellationToken cancellationToken)
        => pipeline.ExecuteAsync(ct => new ValueTask(PublicarUnaVezAsync(mensaje, ct)), cancellationToken).AsTask();

    private async Task PublicarUnaVezAsync(MensajeNotificacion mensaje, CancellationToken cancellationToken)
    {
        var conn = await conexion.ObtenerAsync(cancellationToken);
        await using var canal = await conn.CreateChannelAsync(CanalConConfirmaciones, cancellationToken);

        await canal.ExchangeDeclareAsync(Intercambio, ExchangeType.Direct, durable: true, autoDelete: false,
            arguments: null, cancellationToken: cancellationToken);
        await canal.QueueDeclareAsync(Cola, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: cancellationToken);
        await canal.QueueBindAsync(Cola, Intercambio, ClaveEnrutamiento, arguments: null, cancellationToken: cancellationToken);

        var fechaFinUtc = DateTime.SpecifyKind(mensaje.FechaFin, DateTimeKind.Utc);
        var cuerpo = JsonSerializer.SerializeToUtf8Bytes(
            new { mensaje.IdCarga, mensaje.Usuario, FechaFin = fechaFinUtc.ToString("O") }, Json);
        var propiedades = new BasicProperties { Persistent = true, ContentType = "application/json" };
        await canal.BasicPublishAsync(Intercambio, ClaveEnrutamiento, mandatory: true, basicProperties: propiedades,
            body: cuerpo, cancellationToken: cancellationToken);
    }
}

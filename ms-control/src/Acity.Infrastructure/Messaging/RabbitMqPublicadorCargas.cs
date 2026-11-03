using System.Text.Json;
using Acity.Application.Interfaces.Services;
using Acity.Infrastructure.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using RabbitMQ.Client;

namespace Acity.Infrastructure.Messaging;

/// <summary>
/// Publica en el intercambio directo <c>carga_masiva</c> con confirmación del intermediario (publisher confirms con
/// seguimiento): la llamada completa cuando el broker confirma y lanza ante un rechazo o un mensaje no enrutable.
/// Conexión única y diferida (no se abre al arrancar); un canal por publicación.
/// </summary>
public sealed class RabbitMqPublicadorCargas(
    IOptions<RabbitMqOptions> options,
    [FromKeyedServices(PipelinesResiliencia.RabbitMq)] ResiliencePipeline pipeline) : IPublicadorCargas, IAsyncDisposable
{
    public const string Intercambio = "carga_masiva";
    public const string Cola = "carga_masiva";
    public const string ClaveEnrutamiento = "carga_masiva";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly CreateChannelOptions CanalConConfirmaciones =
        new(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);

    private readonly SemaphoreSlim _cerrojoConexion = new(1, 1);
    private IConnection? _conexion;

    public Task PublicarAsync(MensajeCarga mensaje, CancellationToken cancellationToken)
        => pipeline.ExecuteAsync(ct => new ValueTask(PublicarUnaVezAsync(mensaje, ct)), cancellationToken).AsTask();

    private async Task PublicarUnaVezAsync(MensajeCarga mensaje, CancellationToken cancellationToken)
    {
        var conexion = await ObtenerConexionAsync(cancellationToken);
        await using var canal = await conexion.CreateChannelAsync(CanalConConfirmaciones, cancellationToken);

        // Topología idempotente: el mensaje no se pierde aunque el consumidor aún no exista.
        await canal.ExchangeDeclareAsync(Intercambio, ExchangeType.Direct, durable: true, autoDelete: false,
            arguments: null, cancellationToken: cancellationToken);
        await canal.QueueDeclareAsync(Cola, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: cancellationToken);
        await canal.QueueBindAsync(Cola, Intercambio, ClaveEnrutamiento, arguments: null, cancellationToken: cancellationToken);

        var cuerpo = JsonSerializer.SerializeToUtf8Bytes(mensaje, Json);
        var propiedades = new BasicProperties { Persistent = true, ContentType = "application/json" };
        await canal.BasicPublishAsync(Intercambio, ClaveEnrutamiento, mandatory: true, basicProperties: propiedades,
            body: cuerpo, cancellationToken: cancellationToken);
    }

    private async Task<IConnection> ObtenerConexionAsync(CancellationToken cancellationToken)
    {
        if (_conexion is { IsOpen: true } abierta)
        {
            return abierta;
        }

        await _cerrojoConexion.WaitAsync(cancellationToken);
        try
        {
            if (_conexion is { IsOpen: true } existente)
            {
                return existente;
            }

            if (_conexion is not null)
            {
                await _conexion.DisposeAsync();
            }

            var o = options.Value;
            var fabrica = new ConnectionFactory
            {
                HostName = o.Host,
                Port = o.Puerto,
                UserName = o.Usuario,
                Password = o.Clave,
                ClientProvidedName = "ms-control"
            };
            _conexion = await fabrica.CreateConnectionAsync(cancellationToken);
            return _conexion;
        }
        finally
        {
            _cerrojoConexion.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_conexion is not null)
        {
            await _conexion.DisposeAsync();
        }

        _cerrojoConexion.Dispose();
    }
}

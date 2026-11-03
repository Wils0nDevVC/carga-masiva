using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Acity.Infrastructure.Messaging;

/// <summary>Conexión única a RabbitMQ, creada de forma diferida (no al arrancar) y recreada si se cerró.</summary>
public sealed class RabbitMqConexion(IOptions<RabbitMqOptions> options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _cerrojo = new(1, 1);
    private IConnection? _conexion;

    public async Task<IConnection> ObtenerAsync(CancellationToken cancellationToken)
    {
        if (_conexion is { IsOpen: true } abierta)
        {
            return abierta;
        }

        await _cerrojo.WaitAsync(cancellationToken);
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
                ClientProvidedName = "ms-notificaciones"
            };
            _conexion = await fabrica.CreateConnectionAsync(cancellationToken);
            return _conexion;
        }
        finally
        {
            _cerrojo.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_conexion is not null)
        {
            await _conexion.DisposeAsync();
        }

        _cerrojo.Dispose();
    }
}

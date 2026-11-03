using System.Net;
using System.Net.Http.Headers;
using Acity.Application.Interfaces.Services;
using Acity.Infrastructure.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Acity.Infrastructure.Storage;

/// <summary>
/// Sube el archivo al filer de SeaweedFS con PUT sobre una ruta fija: crear o sobrescribir (respuesta 201),
/// de modo que repetir la subida no duplica archivos. Protegido por el pipeline de SeaweedFS.
/// </summary>
public sealed class SeaweedFsAlmacenamiento(
    HttpClient httpClient,
    IOptions<SeaweedFsOptions> options,
    [FromKeyedServices(PipelinesResiliencia.SeaweedFs)] ResiliencePipeline pipeline) : IAlmacenamientoArchivos
{
    private const string Esquema = "seaweed://";
    private const string TipoXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task SubirAsync(string rutaArchivo, Func<Stream> abrirContenido, CancellationToken cancellationToken)
    {
        var destino = ConstruirUri(rutaArchivo);
        await pipeline.ExecuteAsync(async ct =>
        {
            // Cada intento abre un flujo nuevo del archivo recibido.
            await using var contenido = abrirContenido();
            using var cuerpo = new StreamContent(contenido);
            cuerpo.Headers.ContentType = new MediaTypeHeaderValue(TipoXlsx);
            using var respuesta = await httpClient.PutAsync(destino, cuerpo, ct);
            respuesta.EnsureSuccessStatusCode();
        }, cancellationToken);
    }

    /// <summary>
    /// GET al filer dentro del mismo pipeline (idempotente). 404 = archivo ausente, sin reintentos; un fallo final de
    /// acceso (red, 5xx, tiempo de espera o circuito abierto) se traduce a almacenamiento no disponible.
    /// </summary>
    public async Task<byte[]> DescargarAsync(string rutaArchivo, CancellationToken cancellationToken)
    {
        var origen = ConstruirUri(rutaArchivo);
        try
        {
            return await pipeline.ExecuteAsync(async ct =>
            {
                using var respuesta = await httpClient.GetAsync(origen, ct);
                if (respuesta.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new ArchivoNoEncontradoException(rutaArchivo);
                }

                respuesta.EnsureSuccessStatusCode();
                return await respuesta.Content.ReadAsByteArrayAsync(ct);
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutRejectedException or BrokenCircuitException
                                       or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new AlmacenamientoNoDisponibleException(ex);
        }
    }

    private Uri ConstruirUri(string rutaArchivo)
    {
        if (!rutaArchivo.StartsWith(Esquema, StringComparison.Ordinal))
        {
            throw new ArgumentException("La ruta del archivo debe usar el esquema seaweed://.", nameof(rutaArchivo));
        }

        var baseFiler = options.Value.FilerUrl.TrimEnd('/');
        return new Uri($"{baseFiler}/{rutaArchivo[Esquema.Length..]}");
    }
}

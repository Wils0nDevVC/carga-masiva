using System.Net;
using Acity.Application.Interfaces.Services;
using Acity.Infrastructure.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;

namespace Acity.Infrastructure.Storage;

/// <summary>
/// Descarga el archivo del filer de SeaweedFS. Un 404 es una ausencia confirmada (sin reintentos); los errores
/// transitorios de acceso los reintenta el pipeline de descarga.
/// </summary>
public sealed class SeaweedFsAlmacenamiento(
    HttpClient httpClient,
    IOptions<SeaweedFsOptions> options,
    [FromKeyedServices(PipelinesResiliencia.Descarga)] ResiliencePipeline pipeline) : IAlmacenamientoArchivos
{
    private const string Esquema = "seaweed://";

    public async Task<byte[]> DescargarAsync(string rutaArchivo, CancellationToken cancellationToken)
    {
        var origen = ConstruirUri(rutaArchivo);
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

    private Uri ConstruirUri(string rutaArchivo)
    {
        if (!rutaArchivo.StartsWith(Esquema, StringComparison.Ordinal))
        {
            throw new ArgumentException("La ruta del archivo debe usar el esquema seaweed://.", nameof(rutaArchivo));
        }

        return new Uri($"{options.Value.FilerUrl.TrimEnd('/')}/{rutaArchivo[Esquema.Length..]}");
    }
}

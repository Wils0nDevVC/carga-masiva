using System.Net.Http.Headers;
using Acity.Application.Interfaces.Services;
using Acity.Infrastructure.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;

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

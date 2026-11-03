using Acity.Application.Cargas.Queries.Dtos;
using Acity.Application.Cargas.Queries.ObtenerDetalleCarga;
using Acity.Application.Interfaces.Repositories;
using Acity.Application.Interfaces.Services;
using Acity.Common.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Acity.Application.Cargas.Queries.ObtenerContenidoCarga;

/// <summary>
/// Filas del Excel original (Feature 010, Q1 = B). Solo lectura. La autorización se comprueba antes de leer el archivo;
/// los errores del archivo siguen Q2 (422, 500, 503) y nunca se presentan como contenido vacío.
/// </summary>
public sealed class ObtenerContenidoCargaQueryHandler(
    ICargasConsulta consulta,
    IAlmacenamientoArchivos almacenamiento,
    ILectorContenidoExcel lector,
    ILogger<ObtenerContenidoCargaQueryHandler> logger) : IRequestHandler<ObtenerContenidoCargaQuery, Response<ContenidoArchivoDto>>
{
    public const string MensajeIlegible = "El archivo no se puede leer como Excel.";
    public const string MensajeNoDisponible = "El archivo no está disponible temporalmente. Intente nuevamente más tarde.";
    public const string MensajeAusente = "No se pudo recuperar el archivo asociado a la carga.";

    public async Task<Response<ContenidoArchivoDto>> Handle(ObtenerContenidoCargaQuery request, CancellationToken cancellationToken)
    {
        var archivo = await consulta.ObtenerArchivoAsync(request.IdCarga, request.UsuarioId, request.EsAdministrador, cancellationToken);
        if (archivo is null)
        {
            return Response<ContenidoArchivoDto>.Fail(404, ObtenerDetalleCargaQueryHandler.MensajeNoExiste);
        }

        byte[] bytes;
        try
        {
            bytes = await almacenamiento.DescargarAsync(archivo.RutaArchivo, cancellationToken);
        }
        catch (ArchivoNoEncontradoException ex)
        {
            logger.LogError(ex, "Archivo ausente en la ruta registrada. IdCarga {IdCarga}", request.IdCarga);
            return Response<ContenidoArchivoDto>.Fail(500, MensajeAusente);
        }
        catch (AlmacenamientoNoDisponibleException ex)
        {
            logger.LogWarning("Almacenamiento no disponible al consultar el contenido. IdCarga {IdCarga}, causa {TipoError}",
                request.IdCarga, ex.InnerException?.GetType().Name);
            return Response<ContenidoArchivoDto>.Fail(503, MensajeNoDisponible);
        }

        var contenido = lector.Leer(bytes);
        if (contenido is null)
        {
            logger.LogWarning("Archivo ilegible como Excel. IdCarga {IdCarga}", request.IdCarga);
            return Response<ContenidoArchivoDto>.Fail(422, MensajeIlegible);
        }

        return Response<ContenidoArchivoDto>.Ok(new ContenidoArchivoDto(
            request.IdCarga, archivo.NombreArchivo, contenido.Hoja, contenido.Encabezados, contenido.Filas));
    }
}

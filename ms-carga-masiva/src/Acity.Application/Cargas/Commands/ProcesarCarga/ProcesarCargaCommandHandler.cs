using Acity.Application.Cargas.Procesamiento;
using Acity.Application.Interfaces.Repositories;
using Acity.Application.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Acity.Application.Cargas.Commands.ProcesarCarga;

/// <summary>
/// Toma una carga Pendiente con <c>usp_CambiarEstadoCarga</c> y origen esperado Pendiente (V003): si Control ya la dejó
/// Fallido, o ya se tomó, el mensaje se descarta sin cambios (F001 R-02 punto 6; F005 FR-023).
/// </summary>
public sealed class ProcesarCargaCommandHandler(
    IRepositorioProcesamiento repositorio,
    IProcedimientosCarga procedimientos,
    ProcesadorCarga procesador,
    ILogger<ProcesarCargaCommandHandler> logger) : IRequestHandler<ProcesarCargaCommand, ResultadoProcesamiento>
{
    public async Task<ResultadoProcesamiento> Handle(ProcesarCargaCommand request, CancellationToken cancellationToken)
    {
        var carga = await repositorio.ObtenerCargaAsync(request.IdCarga, cancellationToken);
        if (carga is null)
        {
            logger.LogError("Mensaje descartado: la carga {IdCarga} no existe", request.IdCarga);
            return ResultadoProcesamiento.Descartada;
        }

        if (carga.Estado != "Pendiente")
        {
            logger.LogInformation("Mensaje descartado: la carga {IdCarga} está {Estado}", request.IdCarga, carga.Estado);
            return ResultadoProcesamiento.Descartada;
        }

        if (request.RutaArchivoMensaje is not null && request.RutaArchivoMensaje != carga.RutaArchivo)
        {
            logger.LogWarning("La ruta del mensaje de la carga {IdCarga} difiere de la registrada; se usa la registrada", request.IdCarga);
        }

        if (!await procedimientos.CambiarEstadoAsync(carga.Id, "En proceso", null, "Pendiente", CancellationToken.None))
        {
            logger.LogInformation("Mensaje descartado: la carga {IdCarga} ya no estaba Pendiente al tomarla", request.IdCarga);
            return ResultadoProcesamiento.Descartada;
        }

        await procesador.ProcesarAsync(carga.Id, carga.RutaArchivo);
        return ResultadoProcesamiento.Procesada;
    }
}

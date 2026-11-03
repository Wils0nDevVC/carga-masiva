using Acity.Application.Cargas.Procesamiento;
using Acity.Application.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Acity.Application.Cargas.Commands.RecuperarCargas;

/// <summary>
/// Reanuda las cargas En proceso (omite los periodos Completado y cuenta un intento) y completa las Cargado
/// (Finalizado y notificación). No toca Pendiente ni Finalizado (F001 R-02; constitución §8 v3.2.0).
/// </summary>
public sealed class RecuperarCargasCommandHandler(
    IRepositorioProcesamiento repositorio,
    ProcesadorCarga procesador,
    ILogger<RecuperarCargasCommandHandler> logger) : IRequestHandler<RecuperarCargasCommand, int>
{
    public async Task<int> Handle(RecuperarCargasCommand request, CancellationToken cancellationToken)
    {
        var cargas = await repositorio.ObtenerCargasARecuperarAsync(cancellationToken);
        foreach (var carga in cargas)
        {
            logger.LogInformation("Recuperando la carga {IdCarga} ({Estado})", carga.Id, carga.Estado);
            if (carga.Estado == "En proceso")
            {
                await procesador.ProcesarAsync(carga.Id, carga.RutaArchivo);
            }
            else
            {
                await procesador.FinalizarYNotificarAsync(carga.Id, desdeEnProceso: false);
            }
        }

        return cargas.Count;
    }
}

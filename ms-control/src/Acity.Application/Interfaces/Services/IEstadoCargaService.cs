namespace Acity.Application.Interfaces.Services;

/// <summary>Cambios de estado de la carga mediante <c>usp_CambiarEstadoCarga</c> (README §4: procedimientos almacenados).</summary>
public interface IEstadoCargaService
{
    /// <summary>
    /// Pasa la carga a Fallido solo si sigue Pendiente (origen esperado, V003), de forma atómica.
    /// Devuelve <c>false</c> si el estado ya cambió: entonces no se escribe nada.
    /// </summary>
    Task<bool> PasarAFallidoDesdePendienteAsync(long cargaId, CancellationToken cancellationToken);
}

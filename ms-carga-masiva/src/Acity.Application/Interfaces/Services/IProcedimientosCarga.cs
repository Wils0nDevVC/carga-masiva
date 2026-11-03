namespace Acity.Application.Interfaces.Services;

/// <summary>Procedimientos almacenados de la carga (README §4; contrato de F001 y V003). Fuera de transacciones de la aplicación.</summary>
public interface IProcedimientosCarga
{
    /// <summary><c>usp_ReservarPeriodo</c>: devuelve Admitido, Completado, Rechazado o Bloqueado (idempotente).</summary>
    Task<string> ReservarPeriodoAsync(long cargaId, string periodo, CancellationToken cancellationToken);

    /// <summary>
    /// <c>usp_CambiarEstadoCarga</c>: <c>true</c> si se aplicó la transición. Con <paramref name="estadoEsperado"/>,
    /// solo se aplica si el estado actual coincide (atómico).
    /// </summary>
    Task<bool> CambiarEstadoAsync(long cargaId, string nuevoEstado, string? periodoFallido, string? estadoEsperado, CancellationToken cancellationToken);
}

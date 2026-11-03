namespace Acity.Application.Interfaces.Services;

/// <summary><c>usp_CambiarEstadoCarga</c> (README §4; contrato de F001 y V003).</summary>
public interface IProcedimientosCarga
{
    /// <summary><c>true</c> si se aplicó la transición; con <paramref name="estadoEsperado"/>, solo si el estado coincide.</summary>
    Task<bool> CambiarEstadoAsync(long cargaId, string nuevoEstado, string? estadoEsperado, CancellationToken cancellationToken);
}

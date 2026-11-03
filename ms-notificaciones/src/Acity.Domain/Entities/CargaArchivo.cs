namespace Acity.Domain.Entities;

/// <summary>
/// Carga persistida, solo con lo que Notificaciones lee (Feature 007, FR-003): su estado, para enviar únicamente si está
/// Finalizado. El cambio a Notificado lo hace usp_CambiarEstadoCarga.
/// </summary>
public class CargaArchivo
{
    public long Id { get; set; }

    public string Estado { get; set; } = null!;
}

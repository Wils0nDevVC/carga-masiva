namespace Acity.Domain.Entities;

public class CargaEstadoHistorial
{
    public long Id { get; set; }

    public long CargaId { get; set; }

    public string Estado { get; set; } = null!;

    public DateTime FechaCambio { get; set; }
}

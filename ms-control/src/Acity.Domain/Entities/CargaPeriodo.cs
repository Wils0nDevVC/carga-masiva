namespace Acity.Domain.Entities;

/// <summary>
/// Resultado de un periodo de la carga (solo lectura en Control; Feature 010). Columnas mínimas para la consulta de
/// detalle: PeriodoReservado no se mapea.
/// </summary>
public class CargaPeriodo
{
    public long Id { get; set; }

    public long CargaId { get; set; }

    public string Periodo { get; set; } = null!;

    public string Resultado { get; set; } = null!;

    public DateTime FechaResultado { get; set; }
}

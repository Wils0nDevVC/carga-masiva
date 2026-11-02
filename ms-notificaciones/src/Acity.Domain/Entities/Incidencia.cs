namespace Acity.Domain.Entities;

public class Incidencia
{
    public long Id { get; set; }

    public long CargaId { get; set; }

    public long? CargaPeriodoId { get; set; }

    public string Tipo { get; set; } = null!;

    public int? NumeroFila { get; set; }

    public string? CodigoProducto { get; set; }

    public string? PeriodoTexto { get; set; }

    public string Motivo { get; set; } = null!;

    public DateTime FechaRegistro { get; set; }
}

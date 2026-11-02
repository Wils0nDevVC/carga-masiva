namespace Acity.Domain.Entities;

public class CargaPeriodo
{
    public long Id { get; set; }

    public long CargaId { get; set; }

    public string Periodo { get; set; } = null!;

    public string Resultado { get; set; } = null!;

    public string? PeriodoReservado { get; set; }

    public DateTime FechaResultado { get; set; }
}

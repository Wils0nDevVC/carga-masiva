namespace Acity.Domain.Entities;

public class CargaPeriodo
{
    public long Id { get; set; }
    public long CargaId { get; set; }
    public string Periodo { get; set; } = string.Empty;
    public string Resultado { get; set; } = string.Empty;
    public string? PeriodoReservado { get; set; }
    public DateTime FechaResultado { get; set; }
}

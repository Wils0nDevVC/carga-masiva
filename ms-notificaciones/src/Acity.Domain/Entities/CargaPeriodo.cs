namespace Acity.Domain.Entities;

/// <summary>Resultado de un periodo de la carga, solo lectura para el correo (Feature 016). PeriodoReservado no se mapea.</summary>
public class CargaPeriodo
{
    public long Id { get; set; }

    public long CargaId { get; set; }

    public string Periodo { get; set; } = null!;

    public string Resultado { get; set; } = null!;
}

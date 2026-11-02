namespace Acity.Domain.Entities;

public class CargaArchivo
{
    public long Id { get; set; }

    public string NombreArchivo { get; set; } = null!;

    public int UsuarioId { get; set; }

    public DateTime FechaRecepcion { get; set; }

    public string RutaArchivo { get; set; } = null!;

    public string Estado { get; set; } = null!;

    public DateTime? FechaFin { get; set; }

    public int IntentosProceso { get; set; }
}

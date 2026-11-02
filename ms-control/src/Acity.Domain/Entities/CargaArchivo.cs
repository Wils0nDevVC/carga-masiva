namespace Acity.Domain.Entities;

public class CargaArchivo
{
    public long Id { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public int UsuarioId { get; set; }
    public DateTime FechaRecepcion { get; set; }
    public string RutaArchivo { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public DateTime? FechaFin { get; set; }
    public int IntentosProceso { get; set; }
}

namespace Acity.Application.Cargas.Queries.Dtos;

/// <summary>Carga en el historial (README §2). Fechas en UTC; FechaFin null si no terminó.</summary>
public sealed record CargaResumenDto(
    long IdCarga,
    string NombreArchivo,
    string Usuario,
    DateTime FechaRecepcion,
    string Estado,
    DateTime? FechaFin);

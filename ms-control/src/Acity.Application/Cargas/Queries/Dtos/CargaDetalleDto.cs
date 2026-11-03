namespace Acity.Application.Cargas.Queries.Dtos;

/// <summary>Detalle de una carga: datos generales, historial de estados, resultados por periodo e incidencias.</summary>
public sealed record CargaDetalleDto(
    long IdCarga,
    string NombreArchivo,
    string Usuario,
    DateTime FechaRecepcion,
    string Estado,
    DateTime? FechaFin,
    IReadOnlyList<EstadoHistorialDto> Historial,
    IReadOnlyList<ResultadoPeriodoDto> Periodos,
    IReadOnlyList<IncidenciaDto> Incidencias);

public sealed record EstadoHistorialDto(string Estado, DateTime Fecha);

public sealed record ResultadoPeriodoDto(string Periodo, string Resultado, DateTime Fecha);

public sealed record IncidenciaDto(
    string Tipo,
    int? NumeroFila,
    string? CodigoProducto,
    string? PeriodoTexto,
    string Motivo,
    DateTime Fecha);

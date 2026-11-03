namespace Acity.Application.Cargas.Queries.Dtos;

/// <summary>Filas del Excel original (Feature 010, Q1 = B): valores sin normalizar; celdas vacías como null.</summary>
public sealed record ContenidoArchivoDto(
    long IdCarga,
    string NombreArchivo,
    string Hoja,
    IReadOnlyList<string?> Encabezados,
    IReadOnlyList<FilaContenidoDto> Filas);

public sealed record FilaContenidoDto(int NumeroFila, IReadOnlyList<string?> Celdas);

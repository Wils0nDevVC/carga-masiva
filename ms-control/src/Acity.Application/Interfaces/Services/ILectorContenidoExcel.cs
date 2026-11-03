using Acity.Application.Cargas.Queries.Dtos;

namespace Acity.Application.Interfaces.Services;

/// <summary>Lee las filas del Excel tal como están, sin validar ni normalizar (Feature 010, Q1 = B).</summary>
public interface ILectorContenidoExcel
{
    /// <summary>Null si el archivo no es un Excel legible o no tiene hojas.</summary>
    ContenidoExcel? Leer(byte[] contenido);
}

public sealed record ContenidoExcel(string Hoja, IReadOnlyList<string?> Encabezados, IReadOnlyList<FilaContenidoDto> Filas);

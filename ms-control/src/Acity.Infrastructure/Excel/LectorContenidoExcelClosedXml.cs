using System.Globalization;
using Acity.Application.Cargas.Queries.Dtos;
using Acity.Application.Interfaces.Services;
using ClosedXML.Excel;

namespace Acity.Infrastructure.Excel;

/// <summary>
/// Contenido del Excel para consulta (Feature 010, Q1 = B): primera hoja, fila 1 como encabezados y el resto de
/// filas hasta la última usada (incluidas las vacías intermedias). Valores sin normalizar ni aplicar formato visual.
/// No valida encabezados ni interpreta valores; no registra el contenido.
/// </summary>
public sealed class LectorContenidoExcelClosedXml : ILectorContenidoExcel
{
    public ContenidoExcel? Leer(byte[] contenido)
    {
        XLWorkbook libro;
        try
        {
            libro = new XLWorkbook(new MemoryStream(contenido, writable: false));
        }
        catch (Exception)
        {
            return null;
        }

        using (libro)
        {
            if (libro.Worksheets.Count == 0)
            {
                return null;
            }

            var hoja = libro.Worksheets.First();
            var ultimaColumna = hoja.LastColumnUsed()?.ColumnNumber() ?? 0;
            var ultimaFila = hoja.LastRowUsed()?.RowNumber() ?? 0;

            var encabezados = ultimaFila >= 1 ? Valores(hoja.Row(1), ultimaColumna) : [];
            var filas = new List<FilaContenidoDto>(Math.Max(ultimaFila - 1, 0));
            for (var numero = 2; numero <= ultimaFila; numero++)
            {
                filas.Add(new FilaContenidoDto(numero, Valores(hoja.Row(numero), ultimaColumna)));
            }

            return new ContenidoExcel(hoja.Name, encabezados, filas);
        }
    }

    private static List<string?> Valores(IXLRow fila, int ultimaColumna)
    {
        var valores = new List<string?>(ultimaColumna);
        for (var columna = 1; columna <= ultimaColumna; columna++)
        {
            valores.Add(Valor(fila.Cell(columna).Value));
        }

        return valores;
    }

    // Valor almacenado (no el formato visual de Excel), en representación invariante (research D-06).
    private static string? Valor(XLCellValue valor) => valor.Type switch
    {
        XLDataType.Blank => null,
        XLDataType.Text => valor.GetText(),
        XLDataType.Number => valor.GetNumber().ToString("R", CultureInfo.InvariantCulture),
        XLDataType.Boolean => valor.GetBoolean() ? "true" : "false",
        XLDataType.DateTime => valor.GetDateTime().ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
        XLDataType.TimeSpan => valor.GetTimeSpan().ToString("c", CultureInfo.InvariantCulture),
        XLDataType.Error => valor.GetError().ToString(),
        _ => valor.ToString(CultureInfo.InvariantCulture)
    };
}

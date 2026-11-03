using System.Globalization;
using Acity.Application.Cargas.Queries.Dtos;
using Acity.Application.Interfaces.Services;
using ClosedXML.Excel;

namespace Acity.Infrastructure.Excel;

/// <summary>
/// Contenido del Excel para consulta (Feature 010, Q1 = B): primera hoja, fila 1 como encabezados y el resto de
/// filas hasta la última usada (incluidas las vacías intermedias). Valores sin normalizar ni aplicar formato visual.
/// Las fórmulas no se recalculan: se devuelve su resultado guardado o null si no lo tiene (decisión del usuario,
/// 2026-11-03). No valida encabezados ni interpreta valores; no registra el contenido.
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
            // CachedValue no evalúa fórmulas: devuelve el resultado guardado en el archivo o Blank si no lo hay.
            valores.Add(Valor(fila.Cell(columna).CachedValue));
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
        XLDataType.Error => TextoError(valor.GetError()),
        _ => valor.ToString(CultureInfo.InvariantCulture)
    };

    // Texto de Excel del error (research D-06), sin exponer los nombres del enumerado de ClosedXML.
    private static string TextoError(XLError error) => error switch
    {
        XLError.NullValue => "#NULL!",
        XLError.DivisionByZero => "#DIV/0!",
        XLError.IncompatibleValue => "#VALUE!",
        XLError.CellReference => "#REF!",
        XLError.NameNotRecognized => "#NAME?",
        XLError.NumberInvalid => "#NUM!",
        XLError.NoValueAvailable => "#N/A",
        _ => "#ERROR"
    };
}

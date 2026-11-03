using Acity.Application.Cargas.Procesamiento;
using Acity.Application.Interfaces.Services;
using ClosedXML.Excel;

namespace Acity.Infrastructure.Excel;

/// <summary>
/// Lee el Excel con ClosedXML (constitución §12): primera hoja, encabezados de la fila 1 identificados por nombre y
/// filas de datos en orden. Un archivo corrupto, sin hojas o sin los cuatro encabezados es inválido (constitución §7).
/// No interpreta valores ni registra el contenido.
/// </summary>
public sealed class LectorExcelClosedXml : ILectorExcel
{
    private static readonly string[] Encabezados = ["Periodo", "CodigoProducto", "NombreProducto", "Precio"];

    public ResultadoLecturaExcel Leer(byte[] contenido)
    {
        XLWorkbook libro;
        try
        {
            libro = new XLWorkbook(new MemoryStream(contenido, writable: false));
        }
        catch (Exception)
        {
            return ResultadoLecturaExcel.Invalido("No es un Excel legible.");
        }

        using (libro)
        {
            if (libro.Worksheets.Count == 0)
            {
                return ResultadoLecturaExcel.Invalido("El archivo no tiene hojas.");
            }

            var hoja = libro.Worksheets.First();
            var columnas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var celda in hoja.Row(1).CellsUsed())
            {
                var nombre = celda.GetFormattedString().Trim();
                if (Encabezados.Contains(nombre, StringComparer.OrdinalIgnoreCase) && !columnas.ContainsKey(nombre))
                {
                    columnas[nombre] = celda.Address.ColumnNumber;
                }
            }

            var faltantes = Encabezados.Where(e => !columnas.ContainsKey(e)).ToList();
            if (faltantes.Count > 0)
            {
                return ResultadoLecturaExcel.Invalido($"Faltan encabezados: {string.Join(", ", faltantes)}.");
            }

            var ultima = hoja.LastRowUsed()?.RowNumber() ?? 1;
            var filas = new List<FilaExcel>(Math.Max(ultima - 1, 0));
            for (var numero = 2; numero <= ultima; numero++)
            {
                var fila = hoja.Row(numero);
                filas.Add(new FilaExcel(
                    numero,
                    Texto(fila.Cell(columnas["Periodo"])),
                    Texto(fila.Cell(columnas["CodigoProducto"])),
                    Texto(fila.Cell(columnas["NombreProducto"])),
                    Precio(fila.Cell(columnas["Precio"]))));
            }

            return ResultadoLecturaExcel.Valido(filas);
        }
    }

    private static string? Texto(IXLCell celda)
        => celda.DataType == XLDataType.Blank ? null : celda.GetFormattedString();

    private static CeldaPrecio Precio(IXLCell celda) => celda.DataType switch
    {
        XLDataType.Blank => CeldaPrecio.Vacia,
        XLDataType.Number => new CeldaPrecio(celda.GetDouble(), null, false),
        XLDataType.Error => new CeldaPrecio(null, null, true),
        _ => new CeldaPrecio(null, celda.GetFormattedString(), false)
    };
}

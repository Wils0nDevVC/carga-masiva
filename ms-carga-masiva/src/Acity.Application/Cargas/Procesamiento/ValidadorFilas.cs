using System.Globalization;
using System.Text.RegularExpressions;

namespace Acity.Application.Cargas.Procesamiento;

/// <summary>Fila válida y normalizada, lista para la decisión de ganadores.</summary>
public sealed record FilaValida(int NumeroFila, string Periodo, string CodigoProducto, string NombreProducto, decimal Precio);

/// <summary>
/// Fila omitida con su motivo. <see cref="PeriodoValido"/> tiene valor si el periodo de la fila es válido (la
/// incidencia va en la transacción de ese periodo); si no, va con las incidencias sin periodo.
/// </summary>
public sealed record FilaOmitida(int NumeroFila, string? PeriodoValido, string? PeriodoTexto, string? CodigoProducto, string Motivo);

public sealed record ResultadoValidacion(IReadOnlyList<FilaValida> Validas, IReadOnlyList<FilaOmitida> Omitidas);

/// <summary>
/// Normalización y validación de filas según la constitución §7 y F001 (FR-024–028, FR-044, FR-047, R-07, R-08):
/// fila vacía omitida sin incidencia; Periodo y Código obligatorios; Nombre y Precio con valor por defecto si faltan;
/// valores inválidos omitidos con su motivo, sin redondear ni truncar. Clase pura, sin dependencias.
/// </summary>
public static partial class ValidadorFilas
{
    public const int LongitudCodigo = 100;
    public const int LongitudNombre = 200;
    public const int LongitudPeriodoTexto = 50;
    private const string AvisoTruncado = " (valor truncado)";

    /// <summary>Precio DECIMAL(18,2): como máximo 16 dígitos enteros.</summary>
    private static readonly decimal LimiteEntero = 10_000_000_000_000_000m;

    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoPeriodo();

    public static ResultadoValidacion Validar(IEnumerable<FilaExcel> filas)
    {
        var validas = new List<FilaValida>();
        var omitidas = new List<FilaOmitida>();

        foreach (var fila in filas)
        {
            if (EsVacia(fila))
            {
                continue;
            }

            var periodoTexto = fila.Periodo?.Trim();
            var codigo = fila.CodigoProducto?.Trim();
            string? periodo = null;

            if (string.IsNullOrEmpty(periodoTexto))
            {
                omitidas.Add(Omitida(fila, null, fila.Periodo, codigo, "Periodo ausente."));
                continue;
            }

            if (!FormatoPeriodo().IsMatch(periodoTexto))
            {
                omitidas.Add(Omitida(fila, null, fila.Periodo, codigo, "Periodo inválido: se espera AAAA-MM."));
                continue;
            }

            periodo = periodoTexto;

            if (string.IsNullOrEmpty(codigo))
            {
                omitidas.Add(Omitida(fila, periodo, null, null, "Código de producto ausente."));
                continue;
            }

            if (codigo.Length > LongitudCodigo)
            {
                omitidas.Add(Omitida(fila, periodo, null, codigo, $"Código de producto supera {LongitudCodigo} caracteres."));
                continue;
            }

            var nombre = string.IsNullOrWhiteSpace(fila.NombreProducto) ? "Producto " + codigo : fila.NombreProducto;
            if (nombre.Length > LongitudNombre)
            {
                omitidas.Add(Omitida(fila, periodo, null, codigo, $"Nombre de producto supera {LongitudNombre} caracteres."));
                continue;
            }

            var (precio, motivoPrecio) = InterpretarPrecio(fila.Precio);
            if (motivoPrecio is not null)
            {
                omitidas.Add(Omitida(fila, periodo, null, codigo, motivoPrecio));
                continue;
            }

            validas.Add(new FilaValida(fila.NumeroFila, periodo, codigo, nombre, precio));
        }

        return new ResultadoValidacion(validas, omitidas);
    }

    private static bool EsVacia(FilaExcel fila)
        => string.IsNullOrWhiteSpace(fila.Periodo)
           && string.IsNullOrWhiteSpace(fila.CodigoProducto)
           && string.IsNullOrWhiteSpace(fila.NombreProducto)
           && fila.Precio is { Numero: null, EsError: false }
           && string.IsNullOrWhiteSpace(fila.Precio.Texto);

    /// <summary>Precio ausente → 0; número de Excel por su representación decimal más corta; texto en cultura invariante.</summary>
    private static (decimal Precio, string? Motivo) InterpretarPrecio(CeldaPrecio celda)
    {
        if (celda.EsError)
        {
            return (0, "Precio no numérico.");
        }

        decimal valor;
        if (celda.Numero is { } numero)
        {
            // "R" da la representación más corta que reproduce el double (418.27, no 418.269999…).
            if (!decimal.TryParse(numero.ToString("R", CultureInfo.InvariantCulture), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out valor))
            {
                return (0, "Precio no representable en DECIMAL(18,2).");
            }
        }
        else if (string.IsNullOrWhiteSpace(celda.Texto))
        {
            return (0m, null);
        }
        else if (!decimal.TryParse(celda.Texto,
                     NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                     CultureInfo.InvariantCulture, out valor))
        {
            // Sin separador de miles: "1,5" no se interpreta como 15.
            return (0, "Precio no numérico.");
        }

        if (decimal.Round(valor, 2) != valor || Math.Abs(decimal.Truncate(valor)) >= LimiteEntero)
        {
            return (0, "Precio no representable en DECIMAL(18,2).");
        }

        return (valor, null);
    }

    private static FilaOmitida Omitida(FilaExcel fila, string? periodoValido, string? periodoTexto, string? codigo, string motivo)
    {
        var (periodoAuditoria, periodoTruncado) = Truncar(periodoValido is null ? periodoTexto : null, LongitudPeriodoTexto);
        var (codigoAuditoria, codigoTruncado) = Truncar(codigo, LongitudCodigo);
        if (periodoTruncado || codigoTruncado)
        {
            motivo += AvisoTruncado;
        }

        return new FilaOmitida(fila.NumeroFila, periodoValido, periodoAuditoria, codigoAuditoria, motivo);
    }

    private static (string? Valor, bool Truncado) Truncar(string? valor, int longitud)
        => valor is not null && valor.Length > longitud ? (valor[..longitud], true) : (valor, false);
}

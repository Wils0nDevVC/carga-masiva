using Acity.Application.Cargas.Procesamiento;

namespace Acity.Application.Interfaces.Services;

/// <summary>Lectura del Excel recibido (ClosedXML). No aplica reglas de negocio: solo extrae las celdas.</summary>
public interface ILectorExcel
{
    ResultadoLecturaExcel Leer(byte[] contenido);
}

/// <summary>Filas leídas en el orden del archivo, o el motivo por el que el archivo es inválido.</summary>
public sealed record ResultadoLecturaExcel(IReadOnlyList<FilaExcel> Filas, string? MotivoArchivoInvalido)
{
    public static ResultadoLecturaExcel Valido(IReadOnlyList<FilaExcel> filas) => new(filas, null);

    public static ResultadoLecturaExcel Invalido(string motivo) => new([], motivo);
}

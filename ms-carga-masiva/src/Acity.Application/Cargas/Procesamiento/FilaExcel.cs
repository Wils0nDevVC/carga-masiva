namespace Acity.Application.Cargas.Procesamiento;

/// <summary>Una fila del Excel tal como se leyó (texto mostrado), con su número de fila del archivo.</summary>
public sealed record FilaExcel(int NumeroFila, string? Periodo, string? CodigoProducto, string? NombreProducto, CeldaPrecio Precio);

/// <summary>Celda de precio sin interpretar: número de Excel, texto o error de fórmula.</summary>
public readonly record struct CeldaPrecio(double? Numero, string? Texto, bool EsError)
{
    public static CeldaPrecio Vacia => new(null, null, false);
}

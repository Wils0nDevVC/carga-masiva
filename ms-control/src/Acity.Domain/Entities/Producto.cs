namespace Acity.Domain.Entities;

public class Producto
{
    public long Id { get; set; }
    public string CodigoProducto { get; set; } = string.Empty;
    public string NombreProducto { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public long CargaPeriodoId { get; set; }
}

namespace Acity.Domain.Entities;

public class Producto
{
    public long Id { get; set; }

    public string CodigoProducto { get; set; } = null!;

    public string NombreProducto { get; set; } = null!;

    public decimal Precio { get; set; }

    public long CargaPeriodoId { get; set; }
}

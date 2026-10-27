namespace AppProductos.Models;

public class DetalleVenta
{
    public int IdDetalleVenta { get; set; }
    public int IdVenta { get; set; }
    public int IdProducto { get; set; }
    public int Cantidad { get; set; }
    // Precio histórico al momento de la venta (puede diferir del precio actual del producto)
    public decimal PrecioUnitario { get; set; }

    public Venta? Venta { get; set; }
    public Producto? Producto { get; set; }
}

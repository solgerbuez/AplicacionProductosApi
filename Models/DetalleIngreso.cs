namespace AppProductos.Models;

public class DetalleIngreso
{
    public int IdDetalleIngreso { get; set; }
    public int IdIngreso { get; set; }
    public int IdProducto { get; set; }
    public int Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }

    public Ingreso? Ingreso { get; set; }
    public Producto? Producto { get; set; }
}

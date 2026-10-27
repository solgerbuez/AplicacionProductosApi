namespace AppProductos.Models;

// Cabecera de una entrada de mercadería (compra a proveedor)
public class Ingreso
{
    public int IdIngreso { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public int IdProveedor { get; set; }
    public int IdUsuario { get; set; }
    public string? NumeroComprobante { get; set; }
    public string? Observaciones { get; set; }

    public Proveedor? Proveedor { get; set; }
    public Usuario? Usuario { get; set; }
    public ICollection<DetalleIngreso> Detalles { get; set; } = new List<DetalleIngreso>();
}

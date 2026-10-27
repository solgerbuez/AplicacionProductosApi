namespace AppProductos.Models;

// Cabecera de la venta. El total NO se guarda: se calcula sumando los detalles
// (guardarlo sería un dato derivado/redundante).
public class Venta
{
    public int IdVenta { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public int IdCliente { get; set; }
    public int IdUsuario { get; set; }          // usuario que registró la venta
    public EstadoVenta Estado { get; set; } = EstadoVenta.Pendiente;
    public string? Observaciones { get; set; }

    public Cliente? Cliente { get; set; }
    public Usuario? Usuario { get; set; }
    public ICollection<DetalleVenta> Detalles { get; set; } = new List<DetalleVenta>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
}

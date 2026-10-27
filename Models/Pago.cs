namespace AppProductos.Models;

public class Pago
{
    public int IdPago { get; set; }
    public int IdVenta { get; set; }
    public int IdMetodoPago { get; set; }
    public decimal Monto { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public EstadoPago Estado { get; set; }
    public string CodigoTransaccion { get; set; } = string.Empty;   // simulado

    public Venta? Venta { get; set; }
    public MetodoPago? MetodoPago { get; set; }
}

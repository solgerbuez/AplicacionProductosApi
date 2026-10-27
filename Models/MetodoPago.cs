namespace AppProductos.Models;

public class MetodoPago
{
    public int IdMetodoPago { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
}

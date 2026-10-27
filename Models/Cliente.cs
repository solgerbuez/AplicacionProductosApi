namespace AppProductos.Models;

public class Cliente
{
    public int IdCliente { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string? Dni { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public int? IdUsuario { get; set; }     // cuenta de acceso (si se registró en la app)

    public Usuario? Usuario { get; set; }
    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
}

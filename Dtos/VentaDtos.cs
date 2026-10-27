using System.ComponentModel.DataAnnotations;

namespace AppProductos.Dtos;

// ---- Lo que manda la app al confirmar el carrito ----
public class VentaItemCreateDto
{
    [Range(1, int.MaxValue)] public int IdProducto { get; set; }
    [Range(1, int.MaxValue)] public int Cantidad { get; set; }
}

public class VentaCreateDto
{
    // Si el que compra es un usuario Cliente se ignora y se usa su propio IdCliente.
    // Si vende un Admin/Operador (venta en mostrador) es obligatorio.
    public int? IdCliente { get; set; }
    [StringLength(500)] public string? Observaciones { get; set; }
    [Required, MinLength(1)] public List<VentaItemCreateDto> Items { get; set; } = new();
}

// ---- Pago simulado ----
public class PagoCreateDto
{
    [Range(1, int.MaxValue)] public int IdMetodoPago { get; set; }
    // Solo para la simulación: no se guarda. Si termina en 0000 el pago se rechaza.
    public string? NumeroTarjeta { get; set; }
}

// ---- Respuestas ----
public class VentaDto
{
    public int IdVenta { get; set; }
    public DateTime Fecha { get; set; }
    public int IdCliente { get; set; }
    public string? ClienteNombre { get; set; }
    public string? UsuarioNombre { get; set; }
    public string Estado { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string? Observaciones { get; set; }
    public List<DetalleVentaDto> Detalles { get; set; } = new();
    public List<PagoDto> Pagos { get; set; } = new();
}

public class DetalleVentaDto
{
    public int IdProducto { get; set; }
    public string? ProductoNombre { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal => Cantidad * PrecioUnitario;
}

public class PagoDto
{
    public int IdPago { get; set; }
    public DateTime Fecha { get; set; }
    public string? MetodoPago { get; set; }
    public decimal Monto { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string CodigoTransaccion { get; set; } = string.Empty;
}

public class MetodoPagoDto
{
    public int IdMetodoPago { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

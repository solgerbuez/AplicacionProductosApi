using System.ComponentModel.DataAnnotations;

namespace AppProductos.Dtos;

public class IngresoItemCreateDto
{
    [Range(1, int.MaxValue)] public int IdProducto { get; set; }
    [Range(1, int.MaxValue)] public int Cantidad { get; set; }
    [Range(0, 99999999)] public decimal CostoUnitario { get; set; }
}

public class IngresoCreateDto
{
    [Range(1, int.MaxValue)] public int IdProveedor { get; set; }
    [StringLength(30)] public string? NumeroComprobante { get; set; }
    [StringLength(500)] public string? Observaciones { get; set; }
    [Required, MinLength(1)] public List<IngresoItemCreateDto> Items { get; set; } = new();
}

public class IngresoDto
{
    public int IdIngreso { get; set; }
    public DateTime Fecha { get; set; }
    public int IdProveedor { get; set; }
    public string? ProveedorNombre { get; set; }
    public string? UsuarioNombre { get; set; }
    public string? NumeroComprobante { get; set; }
    public string? Observaciones { get; set; }
    public decimal Total { get; set; }
    public List<DetalleIngresoDto> Detalles { get; set; } = new();
}

public class DetalleIngresoDto
{
    public int IdProducto { get; set; }
    public string? ProductoNombre { get; set; }
    public int Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal Subtotal => Cantidad * CostoUnitario;
}

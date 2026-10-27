using System.ComponentModel.DataAnnotations;

namespace AppProductos.Dtos;

public class ProductoDto
{
    public int IdProducto { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public int StockMinimo { get; set; }
    public bool Disponible => Activo && Stock > 0;
    public bool StockBajo => Stock <= StockMinimo;
    public int IdCategoria { get; set; }
    public string? CategoriaNombre { get; set; }
    public string? ImagenPrincipalUrl { get; set; }
    public List<ProductoImagenDto> Imagenes { get; set; } = new();
    public bool Activo { get; set; }
}

public class ProductoImagenDto
{
    public int IdProductoImagen { get; set; }
    public string Url { get; set; } = string.Empty;
    public bool EsPrincipal { get; set; }
    public int Orden { get; set; }
}

// El stock NO se carga acá: entra con un Ingreso y sale con una Venta,
// así cada movimiento queda registrado.
public class ProductoCreateDto
{
    [Required, StringLength(150)] public string Nombre { get; set; } = string.Empty;
    [StringLength(1000)] public string? Descripcion { get; set; }
    [Range(0.01, 99999999)] public decimal Precio { get; set; }
    [Range(0, int.MaxValue)] public int StockMinimo { get; set; }
    [Range(1, int.MaxValue)] public int IdCategoria { get; set; }
}

public class ProductoUpdateDto : ProductoCreateDto
{
    public bool Activo { get; set; } = true;
}

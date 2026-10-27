using System.ComponentModel.DataAnnotations;

namespace AppProductos.Dtos;

public class ClienteDto
{
    public int IdCliente { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string? Dni { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public int? IdUsuario { get; set; }
}

public class ClienteCreateDto
{
    [Required, StringLength(80)] public string Nombre { get; set; } = string.Empty;
    [Required, StringLength(80)] public string Apellido { get; set; } = string.Empty;
    [StringLength(15)] public string? Dni { get; set; }
    [EmailAddress] public string? Email { get; set; }
    [StringLength(30)] public string? Telefono { get; set; }
    [StringLength(200)] public string? Direccion { get; set; }
}

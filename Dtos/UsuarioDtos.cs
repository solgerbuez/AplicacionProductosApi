using System.ComponentModel.DataAnnotations;

namespace AppProductos.Dtos;

public class UsuarioDto
{
    public int IdUsuario { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaAlta { get; set; }
}

public class UsuarioCreateDto
{
    [Required, StringLength(50, MinimumLength = 3)] public string Username { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 6)] public string Password { get; set; } = string.Empty;
    [Required] public string Rol { get; set; } = "Operador";   // Admin | Operador | Cliente
    [EmailAddress] public string? Email { get; set; }
}

public class UsuarioUpdateDto
{
    [Required] public string Rol { get; set; } = string.Empty;
    [EmailAddress] public string? Email { get; set; }
    public bool Activo { get; set; }
}

public class RolDto
{
    public int IdRol { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

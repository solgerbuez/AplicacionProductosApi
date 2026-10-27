using System.ComponentModel.DataAnnotations;

namespace AppProductos.Dtos;

public class LoginDto
{
    [Required] public string Username { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expira { get; set; }
    public int IdUsuario { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public int? IdCliente { get; set; }   // viene cargado si el usuario es Cliente
}

// Registro público desde la app (crea Usuario con rol Cliente + su ficha de Cliente)
public class RegistroClienteDto
{
    [Required, StringLength(50, MinimumLength = 3)] public string Username { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 6)] public string Password { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, StringLength(80)] public string Nombre { get; set; } = string.Empty;
    [Required, StringLength(80)] public string Apellido { get; set; } = string.Empty;
    [StringLength(15)] public string? Dni { get; set; }
    [StringLength(30)] public string? Telefono { get; set; }
    [StringLength(200)] public string? Direccion { get; set; }
}

public class CambiarPasswordDto
{
    [Required] public string PasswordActual { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 6)] public string PasswordNueva { get; set; } = string.Empty;
}

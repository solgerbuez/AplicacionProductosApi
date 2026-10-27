using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using AppProductos.Common;
using AppProductos.Data;
using AppProductos.Dtos;
using AppProductos.Models;
using AppProductos.Services;

namespace AppProductos.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;
    private readonly AuditoriaService _auditoria;

    public AuthController(AppDbContext context, IConfiguration config, AuditoriaService auditoria)
    {
        _context = context;
        _config = config;
        _auditoria = auditoria;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginDto dto)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .Include(u => u.Cliente)
            .FirstOrDefaultAsync(u => u.Username == dto.Username && u.Activo);

        if (usuario is null || !BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash))
        {
            await _auditoria.RegistrarAsync("LoginFallido", nameof(Usuario), detalle: $"Usuario: {dto.Username}");
            return Unauthorized("Usuario o contraseña incorrectos.");
        }

        await _auditoria.RegistrarAsync("Login", nameof(Usuario), usuario.IdUsuario.ToString(), idUsuario: usuario.IdUsuario);
        return Ok(GenerarToken(usuario));
    }

    /// <summary>Registro público de clientes desde la app.</summary>
    [HttpPost("registro")]
    public async Task<ActionResult<LoginResponseDto>> Registro(RegistroClienteDto dto)
    {
        if (await _context.Usuarios.AnyAsync(u => u.Username == dto.Username))
            return BadRequest("Ese nombre de usuario ya existe.");

        if (!string.IsNullOrWhiteSpace(dto.Dni) && await _context.Clientes.AnyAsync(c => c.Dni == dto.Dni))
            return BadRequest("Ya existe un cliente con ese DNI.");

        var usuario = new Usuario
        {
            Username = dto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Email = dto.Email,
            IdRol = Roles.IdCliente,
            Cliente = new Cliente
            {
                Nombre = dto.Nombre,
                Apellido = dto.Apellido,
                Dni = string.IsNullOrWhiteSpace(dto.Dni) ? null : dto.Dni,
                Email = dto.Email,
                Telefono = dto.Telefono,
                Direccion = dto.Direccion
            }
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        await _context.Entry(usuario).Reference(u => u.Rol).LoadAsync();
        return Ok(GenerarToken(usuario));
    }

    /// <summary>Datos del usuario logueado (útil para la app al iniciar).</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<LoginResponseDto>> Me()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var u = await _context.Usuarios.Include(x => x.Rol).Include(x => x.Cliente)
            .AsNoTracking().FirstOrDefaultAsync(x => x.IdUsuario == id);
        if (u is null) return NotFound();

        return Ok(new LoginResponseDto
        {
            IdUsuario = u.IdUsuario,
            Username = u.Username,
            Rol = u.Rol!.Nombre,
            IdCliente = u.Cliente?.IdCliente
        });
    }

    [Authorize]
    [HttpPut("cambiar-password")]
    public async Task<IActionResult> CambiarPassword(CambiarPasswordDto dto)
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario is null) return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(dto.PasswordActual, usuario.PasswordHash))
            return BadRequest("La contraseña actual no es correcta.");

        usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.PasswordNueva);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private LoginResponseDto GenerarToken(Usuario usuario)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()),
            new(ClaimTypes.Name, usuario.Username),
            new(ClaimTypes.Role, usuario.Rol!.Nombre)
        };
        if (usuario.Cliente is not null)
            claims.Add(new Claim("idCliente", usuario.Cliente.IdCliente.ToString()));

        var expira = DateTime.UtcNow.AddMinutes(double.Parse(_config["Jwt:ExpiresInMinutes"] ?? "120"));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expira,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!)),
                SecurityAlgorithms.HmacSha256));

        return new LoginResponseDto
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            Expira = expira,
            IdUsuario = usuario.IdUsuario,
            Username = usuario.Username,
            Rol = usuario.Rol.Nombre,
            IdCliente = usuario.Cliente?.IdCliente
        };
    }
}

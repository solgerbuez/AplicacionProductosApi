using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppProductos.Common;
using AppProductos.Data;
using AppProductos.Dtos;
using AppProductos.Models;

namespace AppProductos.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Admin)]
public class UsuariosController : ControllerBase
{
    private readonly AppDbContext _context;
    public UsuariosController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<PagedResult<UsuarioDto>>> GetAll(int page = 1, int pageSize = 10, string? rol = null)
    {
        var query = _context.Usuarios.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(rol)) query = query.Where(u => u.Rol!.Nombre == rol);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(u => u.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UsuarioDto
            {
                IdUsuario = u.IdUsuario, Username = u.Username, Rol = u.Rol!.Nombre,
                Email = u.Email, Activo = u.Activo, FechaAlta = u.FechaAlta
            })
            .ToListAsync();

        return Ok(new PagedResult<UsuarioDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UsuarioDto>> GetById(int id)
    {
        var u = await _context.Usuarios.Include(x => x.Rol).AsNoTracking().FirstOrDefaultAsync(x => x.IdUsuario == id);
        if (u is null) return NotFound();
        return Ok(new UsuarioDto { IdUsuario = u.IdUsuario, Username = u.Username, Rol = u.Rol!.Nombre, Email = u.Email, Activo = u.Activo, FechaAlta = u.FechaAlta });
    }

    [HttpGet("roles")]
    public async Task<ActionResult<List<RolDto>>> GetRoles() =>
        Ok(await _context.Roles.AsNoTracking().OrderBy(r => r.IdRol)
            .Select(r => new RolDto { IdRol = r.IdRol, Nombre = r.Nombre }).ToListAsync());

    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Create(UsuarioCreateDto dto)
    {
        if (await _context.Usuarios.AnyAsync(u => u.Username == dto.Username))
            return BadRequest("Ese nombre de usuario ya existe.");

        var rol = await _context.Roles.FirstOrDefaultAsync(r => r.Nombre == dto.Rol);
        if (rol is null) return BadRequest("Rol inválido. Valores posibles: Admin, Operador, Cliente.");

        var usuario = new Usuario
        {
            Username = dto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            IdRol = rol.IdRol,
            Email = dto.Email
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = usuario.IdUsuario },
            new UsuarioDto { IdUsuario = usuario.IdUsuario, Username = usuario.Username, Rol = rol.Nombre, Email = usuario.Email, Activo = usuario.Activo, FechaAlta = usuario.FechaAlta });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UsuarioUpdateDto dto)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario is null) return NotFound();

        var rol = await _context.Roles.FirstOrDefaultAsync(r => r.Nombre == dto.Rol);
        if (rol is null) return BadRequest("Rol inválido. Valores posibles: Admin, Operador, Cliente.");

        usuario.IdRol = rol.IdRol;
        usuario.Email = dto.Email;
        usuario.Activo = dto.Activo;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // Baja lógica
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario is null) return NotFound();

        usuario.Activo = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

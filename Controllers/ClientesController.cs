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
[Authorize(Roles = Roles.Staff)]
public class ClientesController : ControllerBase
{
    private readonly AppDbContext _context;
    public ClientesController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<PagedResult<ClienteDto>>> GetAll(int page = 1, int pageSize = 10, string? busqueda = null)
    {
        var query = _context.Clientes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(busqueda))
            query = query.Where(c => c.Nombre.Contains(busqueda) || c.Apellido.Contains(busqueda) || (c.Dni != null && c.Dni.Contains(busqueda)));

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(c => c.Apellido).ThenBy(c => c.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => MapToDto(c))
            .ToListAsync();

        return Ok(new PagedResult<ClienteDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ClienteDto>> GetById(int id)
    {
        var c = await _context.Clientes.FindAsync(id);
        if (c is null) return NotFound();
        return Ok(MapToDto(c));
    }

    [HttpPost]
    public async Task<ActionResult<ClienteDto>> Create(ClienteCreateDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.Dni) && await _context.Clientes.AnyAsync(c => c.Dni == dto.Dni))
            return BadRequest("Ya existe un cliente con ese DNI.");

        var cliente = new Cliente();
        Copiar(dto, cliente);

        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = cliente.IdCliente }, MapToDto(cliente));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, ClienteCreateDto dto)
    {
        var cliente = await _context.Clientes.FindAsync(id);
        if (cliente is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(dto.Dni) && await _context.Clientes.AnyAsync(c => c.Dni == dto.Dni && c.IdCliente != id))
            return BadRequest("Ya existe un cliente con ese DNI.");

        Copiar(dto, cliente);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var cliente = await _context.Clientes.FindAsync(id);
        if (cliente is null) return NotFound();

        if (await _context.Ventas.AnyAsync(v => v.IdCliente == id))
            return Conflict("No se puede eliminar: el cliente tiene ventas registradas.");

        _context.Clientes.Remove(cliente);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    internal static void Copiar(ClienteCreateDto dto, Cliente c)
    {
        c.Nombre = dto.Nombre;
        c.Apellido = dto.Apellido;
        c.Dni = string.IsNullOrWhiteSpace(dto.Dni) ? null : dto.Dni;
        c.Email = dto.Email;
        c.Telefono = dto.Telefono;
        c.Direccion = dto.Direccion;
    }

    internal static ClienteDto MapToDto(Cliente c) => new()
    {
        IdCliente = c.IdCliente,
        Nombre = c.Nombre,
        Apellido = c.Apellido,
        Dni = c.Dni,
        Email = c.Email,
        Telefono = c.Telefono,
        Direccion = c.Direccion,
        IdUsuario = c.IdUsuario
    };
}

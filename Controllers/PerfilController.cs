using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppProductos.Common;
using AppProductos.Data;
using AppProductos.Dtos;
using AppProductos.Models;

namespace AppProductos.Controllers;

/// <summary>Perfil del cliente logueado (lo usa la app con rol Cliente).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Cliente)]
public class PerfilController : ControllerBase
{
    private readonly AppDbContext _context;
    public PerfilController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<ClienteDto>> GetMiPerfil()
    {
        var cliente = await BuscarClienteLogueado();
        return cliente is null ? NotFound() : Ok(ClientesController.MapToDto(cliente));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateMiPerfil(ClienteCreateDto dto)
    {
        var cliente = await BuscarClienteLogueado();
        if (cliente is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(dto.Dni) && await _context.Clientes.AnyAsync(c => c.Dni == dto.Dni && c.IdCliente != cliente.IdCliente))
            return BadRequest("Ya existe un cliente con ese DNI.");

        ClientesController.Copiar(dto, cliente);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<Cliente?> BuscarClienteLogueado()
    {
        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return await _context.Clientes.FirstOrDefaultAsync(c => c.IdUsuario == idUsuario);
    }
}

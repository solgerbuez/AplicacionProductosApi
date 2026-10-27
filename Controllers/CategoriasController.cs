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
public class CategoriasController : ControllerBase
{
    private readonly AppDbContext _context;
    public CategoriasController(AppDbContext context) => _context = context;

    // Público: la app lo usa para los filtros del catálogo
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<PagedResult<CategoriaDto>>> GetAll(int page = 1, int pageSize = 50, bool incluirInactivas = false)
    {
        var query = _context.Categorias.AsNoTracking().AsQueryable();
        if (!incluirInactivas) query = query.Where(c => c.Activo);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(c => c.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CategoriaDto { IdCategoria = c.IdCategoria, Nombre = c.Nombre, Descripcion = c.Descripcion, Activo = c.Activo })
            .ToListAsync();

        return Ok(new PagedResult<CategoriaDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total });
    }

    [AllowAnonymous]
    [HttpGet("{id}")]
    public async Task<ActionResult<CategoriaDto>> GetById(int id)
    {
        var c = await _context.Categorias.FindAsync(id);
        if (c is null) return NotFound();
        return Ok(new CategoriaDto { IdCategoria = c.IdCategoria, Nombre = c.Nombre, Descripcion = c.Descripcion, Activo = c.Activo });
    }

    [HttpPost]
    public async Task<ActionResult<CategoriaDto>> Create(CategoriaCreateDto dto)
    {
        if (await _context.Categorias.AnyAsync(c => c.Nombre == dto.Nombre))
            return BadRequest("Ya existe una categoría con ese nombre.");

        var categoria = new Categoria { Nombre = dto.Nombre, Descripcion = dto.Descripcion, Activo = dto.Activo };
        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = categoria.IdCategoria },
            new CategoriaDto { IdCategoria = categoria.IdCategoria, Nombre = categoria.Nombre, Descripcion = categoria.Descripcion, Activo = categoria.Activo });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, CategoriaCreateDto dto)
    {
        var categoria = await _context.Categorias.FindAsync(id);
        if (categoria is null) return NotFound();

        if (await _context.Categorias.AnyAsync(c => c.Nombre == dto.Nombre && c.IdCategoria != id))
            return BadRequest("Ya existe una categoría con ese nombre.");

        categoria.Nombre = dto.Nombre;
        categoria.Descripcion = dto.Descripcion;
        categoria.Activo = dto.Activo;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var categoria = await _context.Categorias.FindAsync(id);
        if (categoria is null) return NotFound();

        if (await _context.Productos.AnyAsync(p => p.IdCategoria == id))
            return Conflict("No se puede eliminar: la categoría tiene productos. Podés desactivarla.");

        _context.Categorias.Remove(categoria);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

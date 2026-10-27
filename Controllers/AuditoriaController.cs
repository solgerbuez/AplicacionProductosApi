using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppProductos.Common;
using AppProductos.Data;
using AppProductos.Models;

namespace AppProductos.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Admin)]
public class AuditoriaController : ControllerBase
{
    private readonly AppDbContext _context;
    public AuditoriaController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<PagedResult<Auditoria>>> GetAuditoria(
        int page = 1, int pageSize = 20, string? entidad = null, string? accion = null,
        int? idUsuario = null, DateTime? desde = null, DateTime? hasta = null)
    {
        var query = _context.Auditorias.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(entidad)) query = query.Where(a => a.Entidad == entidad);
        if (!string.IsNullOrWhiteSpace(accion)) query = query.Where(a => a.Accion == accion);
        if (idUsuario.HasValue) query = query.Where(a => a.IdUsuario == idUsuario);
        if (desde.HasValue) query = query.Where(a => a.Fecha >= desde.Value.Date);
        if (hasta.HasValue) query = query.Where(a => a.Fecha < hasta.Value.Date.AddDays(1));

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(a => a.Fecha).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new PagedResult<Auditoria> { Items = items, Page = page, PageSize = pageSize, TotalCount = total });
    }

    [HttpGet("errores")]
    public async Task<ActionResult<PagedResult<LogError>>> GetErrores(
        int page = 1, int pageSize = 20, DateTime? desde = null, DateTime? hasta = null, string? origen = null)
    {
        var query = _context.LogErrores.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(origen)) query = query.Where(l => l.Origen == origen);
        if (desde.HasValue) query = query.Where(l => l.Fecha >= desde.Value.Date);
        if (hasta.HasValue) query = query.Where(l => l.Fecha < hasta.Value.Date.AddDays(1));

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(l => l.Fecha).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new PagedResult<LogError> { Items = items, Page = page, PageSize = pageSize, TotalCount = total });
    }
}

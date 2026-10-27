using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppProductos.Common;
using AppProductos.Data;
using AppProductos.Dtos;
using AppProductos.Models;

namespace AppProductos.Controllers;

/// <summary>Entradas de mercadería (compras a proveedores). Suman stock.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Staff)]
public class IngresosController : ControllerBase
{
    private readonly AppDbContext _context;
    public IngresosController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<PagedResult<IngresoDto>>> GetAll(
        int page = 1, int pageSize = 10, int? idProveedor = null, int? idProducto = null,
        DateTime? desde = null, DateTime? hasta = null)
    {
        var query = QueryCompleta().AsNoTracking();

        if (idProveedor.HasValue) query = query.Where(i => i.IdProveedor == idProveedor);
        if (idProducto.HasValue) query = query.Where(i => i.Detalles.Any(d => d.IdProducto == idProducto));
        if (desde.HasValue) query = query.Where(i => i.Fecha >= desde.Value.Date);
        if (hasta.HasValue) query = query.Where(i => i.Fecha < hasta.Value.Date.AddDays(1));

        var total = await query.CountAsync();
        var ingresos = await query
            .OrderByDescending(i => i.Fecha)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<IngresoDto> { Items = ingresos.Select(MapToDto).ToList(), Page = page, PageSize = pageSize, TotalCount = total });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<IngresoDto>> GetById(int id)
    {
        var ingreso = await QueryCompleta().AsNoTracking().FirstOrDefaultAsync(i => i.IdIngreso == id);
        if (ingreso is null) return NotFound();
        return Ok(MapToDto(ingreso));
    }

    [HttpPost]
    public async Task<ActionResult<IngresoDto>> Create(IngresoCreateDto dto)
    {
        if (!await _context.Proveedores.AnyAsync(p => p.IdProveedor == dto.IdProveedor))
            return BadRequest("El proveedor indicado no existe.");

        var items = dto.Items
            .GroupBy(i => i.IdProducto)
            .Select(g => new { IdProducto = g.Key, Cantidad = g.Sum(x => x.Cantidad), CostoUnitario = g.Last().CostoUnitario })
            .ToList();

        var ids = items.Select(i => i.IdProducto).ToList();

        await using var tx = await _context.Database.BeginTransactionAsync();

        var productos = await _context.Productos.Where(p => ids.Contains(p.IdProducto)).ToDictionaryAsync(p => p.IdProducto);
        var faltantes = ids.Where(id => !productos.ContainsKey(id)).ToList();
        if (faltantes.Count > 0)
            return BadRequest($"No existen los productos: {string.Join(", ", faltantes)}.");

        var ingreso = new Ingreso
        {
            Fecha = DateTime.Now,
            IdProveedor = dto.IdProveedor,
            IdUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            NumeroComprobante = dto.NumeroComprobante,
            Observaciones = dto.Observaciones
        };

        foreach (var item in items)
        {
            productos[item.IdProducto].Stock += item.Cantidad;
            ingreso.Detalles.Add(new DetalleIngreso
            {
                IdProducto = item.IdProducto,
                Cantidad = item.Cantidad,
                CostoUnitario = item.CostoUnitario
            });
        }

        _context.Ingresos.Add(ingreso);
        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        var creado = await QueryCompleta().AsNoTracking().FirstAsync(i => i.IdIngreso == ingreso.IdIngreso);
        return CreatedAtAction(nameof(GetById), new { id = ingreso.IdIngreso }, MapToDto(creado));
    }

    private IQueryable<Ingreso> QueryCompleta() => _context.Ingresos
        .Include(i => i.Proveedor)
        .Include(i => i.Usuario)
        .Include(i => i.Detalles).ThenInclude(d => d.Producto)
        .AsSplitQuery();

    private static IngresoDto MapToDto(Ingreso i) => new()
    {
        IdIngreso = i.IdIngreso,
        Fecha = i.Fecha,
        IdProveedor = i.IdProveedor,
        ProveedorNombre = i.Proveedor?.RazonSocial,
        UsuarioNombre = i.Usuario?.Username,
        NumeroComprobante = i.NumeroComprobante,
        Observaciones = i.Observaciones,
        Total = i.Detalles.Sum(d => d.Cantidad * d.CostoUnitario),
        Detalles = i.Detalles.Select(d => new DetalleIngresoDto
        {
            IdProducto = d.IdProducto,
            ProductoNombre = d.Producto?.Nombre,
            Cantidad = d.Cantidad,
            CostoUnitario = d.CostoUnitario
        }).ToList()
    };
}

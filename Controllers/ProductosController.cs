using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppProductos.Common;
using AppProductos.Data;
using AppProductos.Dtos;
using AppProductos.Models;
using AppProductos.Services;

namespace AppProductos.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Staff)]
public class ProductosController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ImagenService _imagenes;

    public ProductosController(AppDbContext context, ImagenService imagenes)
    {
        _context = context;
        _imagenes = imagenes;
    }

    // ===================== CATÁLOGO (público) =====================

    /// <param name="orden">nombre | precio_asc | precio_desc | recientes</param>
    /// <param name="soloDisponibles">true = solo con stock (vista de catálogo)</param>
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductoDto>>> GetAll(
        int page = 1, int pageSize = 10, int? idCategoria = null, string? busqueda = null,
        decimal? precioMin = null, decimal? precioMax = null,
        bool soloDisponibles = false, bool incluirInactivos = false, string? orden = null)
    {
        var query = _context.Productos.AsNoTracking().AsQueryable();

        // Los inactivos solo los puede pedir el personal
        if (!incluirInactivos || !EsStaff()) query = query.Where(p => p.Activo);
        if (soloDisponibles) query = query.Where(p => p.Stock > 0);
        if (idCategoria.HasValue) query = query.Where(p => p.IdCategoria == idCategoria);
        if (precioMin.HasValue) query = query.Where(p => p.Precio >= precioMin);
        if (precioMax.HasValue) query = query.Where(p => p.Precio <= precioMax);
        if (!string.IsNullOrWhiteSpace(busqueda))
            query = query.Where(p => p.Nombre.Contains(busqueda) || (p.Descripcion != null && p.Descripcion.Contains(busqueda)));

        query = orden switch
        {
            "precio_asc" => query.OrderBy(p => p.Precio),
            "precio_desc" => query.OrderByDescending(p => p.Precio),
            "recientes" => query.OrderByDescending(p => p.FechaCreacion),
            _ => query.OrderBy(p => p.Nombre)
        };

        var total = await query.CountAsync();
        var productos = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(p => p.Categoria)
            .Include(p => p.Imagenes)
            .ToListAsync();

        return Ok(new PagedResult<ProductoDto>
        {
            Items = productos.Select(MapToDto).ToList(),
            Page = page, PageSize = pageSize, TotalCount = total
        });
    }

    [AllowAnonymous]
    [HttpGet("{id}")]
    public async Task<ActionResult<ProductoDto>> GetById(int id)
    {
        var p = await _context.Productos.AsNoTracking()
            .Include(x => x.Categoria)
            .Include(x => x.Imagenes)
            .FirstOrDefaultAsync(x => x.IdProducto == id);

        if (p is null || (!p.Activo && !EsStaff())) return NotFound();
        return Ok(MapToDto(p));
    }

    /// <summary>Productos con stock en o por debajo del mínimo (para alertas / reportes).</summary>
    [HttpGet("stock-bajo")]
    public async Task<ActionResult<List<ProductoDto>>> GetStockBajo()
    {
        var productos = await _context.Productos.AsNoTracking()
            .Where(p => p.Activo && p.Stock <= p.StockMinimo)
            .Include(p => p.Categoria).Include(p => p.Imagenes)
            .OrderBy(p => p.Stock)
            .ToListAsync();
        return Ok(productos.Select(MapToDto).ToList());
    }

    // ===================== ABM (Admin / Operador) =====================

    [HttpPost]
    public async Task<ActionResult<ProductoDto>> Create(ProductoCreateDto dto)
    {
        if (!await _context.Categorias.AnyAsync(c => c.IdCategoria == dto.IdCategoria && c.Activo))
            return BadRequest("La categoría indicada no existe o está inactiva.");

        var producto = new Producto
        {
            Nombre = dto.Nombre,
            Descripcion = dto.Descripcion,
            Precio = dto.Precio,
            StockMinimo = dto.StockMinimo,
            IdCategoria = dto.IdCategoria,
            Stock = 0
        };

        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();
        await _context.Entry(producto).Reference(p => p.Categoria).LoadAsync();

        return CreatedAtAction(nameof(GetById), new { id = producto.IdProducto }, MapToDto(producto));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, ProductoUpdateDto dto)
    {
        var producto = await _context.Productos.FindAsync(id);
        if (producto is null) return NotFound();

        if (!await _context.Categorias.AnyAsync(c => c.IdCategoria == dto.IdCategoria))
            return BadRequest("La categoría indicada no existe.");

        producto.Nombre = dto.Nombre;
        producto.Descripcion = dto.Descripcion;
        producto.Precio = dto.Precio;
        producto.StockMinimo = dto.StockMinimo;
        producto.IdCategoria = dto.IdCategoria;
        producto.Activo = dto.Activo;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // Baja lógica (el producto puede estar en ventas/ingresos históricos)
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var producto = await _context.Productos.FindAsync(id);
        if (producto is null) return NotFound();

        producto.Activo = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ===================== IMÁGENES =====================

    /// <summary>
    /// Sube una imagen al producto. Postman: Body > form-data > clave "archivo" (tipo File).
    /// La primera imagen que se sube queda como principal.
    /// </summary>
    [HttpPost("{id}/imagenes")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImagenService.TamanioMaximo + 1024 * 1024)]
    public async Task<ActionResult<ProductoImagenDto>> SubirImagen(int id, IFormFile? archivo, [FromForm] bool esPrincipal = false)
    {
        var producto = await _context.Productos.Include(p => p.Imagenes).FirstOrDefaultAsync(p => p.IdProducto == id);
        if (producto is null) return NotFound();

        var (ok, nombreArchivo, error) = await _imagenes.GuardarAsync(archivo);
        if (!ok) return BadRequest(error);

        var esLaPrimera = producto.Imagenes.Count == 0;
        if (esPrincipal && !esLaPrimera)
            foreach (var img in producto.Imagenes) img.EsPrincipal = false;

        var imagen = new ProductoImagen
        {
            NombreArchivo = nombreArchivo!,
            EsPrincipal = esPrincipal || esLaPrimera,
            Orden = esLaPrimera ? 0 : producto.Imagenes.Max(i => i.Orden) + 1
        };
        producto.Imagenes.Add(imagen);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            _imagenes.Eliminar(nombreArchivo!);   // no dejar archivos huérfanos
            throw;
        }

        return Ok(MapImagen(imagen));
    }

    [HttpPut("{id}/imagenes/{idImagen}/principal")]
    public async Task<IActionResult> MarcarPrincipal(int id, int idImagen)
    {
        var imagenes = await _context.ProductoImagenes.Where(i => i.IdProducto == id).ToListAsync();
        if (!imagenes.Any(i => i.IdProductoImagen == idImagen)) return NotFound();

        foreach (var img in imagenes) img.EsPrincipal = img.IdProductoImagen == idImagen;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}/imagenes/{idImagen}")]
    public async Task<IActionResult> EliminarImagen(int id, int idImagen)
    {
        var imagen = await _context.ProductoImagenes.FirstOrDefaultAsync(i => i.IdProducto == id && i.IdProductoImagen == idImagen);
        if (imagen is null) return NotFound();

        _context.ProductoImagenes.Remove(imagen);

        // Si era la principal, pasa a ser principal la siguiente
        if (imagen.EsPrincipal)
        {
            var siguiente = await _context.ProductoImagenes
                .Where(i => i.IdProducto == id && i.IdProductoImagen != idImagen)
                .OrderBy(i => i.Orden).FirstOrDefaultAsync();
            if (siguiente is not null) siguiente.EsPrincipal = true;
        }

        await _context.SaveChangesAsync();
        _imagenes.Eliminar(imagen.NombreArchivo);
        return NoContent();
    }

    // ===================== Helpers =====================

    private bool EsStaff() => User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Operador);

    private ProductoImagenDto MapImagen(ProductoImagen i) => new()
    {
        IdProductoImagen = i.IdProductoImagen,
        Url = ImagenService.ArmarUrl(Request, i.NombreArchivo),
        EsPrincipal = i.EsPrincipal,
        Orden = i.Orden
    };

    private ProductoDto MapToDto(Producto p)
    {
        var imagenes = p.Imagenes.OrderByDescending(i => i.EsPrincipal).ThenBy(i => i.Orden).Select(MapImagen).ToList();
        return new ProductoDto
        {
            IdProducto = p.IdProducto,
            Nombre = p.Nombre,
            Descripcion = p.Descripcion,
            Precio = p.Precio,
            Stock = p.Stock,
            StockMinimo = p.StockMinimo,
            IdCategoria = p.IdCategoria,
            CategoriaNombre = p.Categoria?.Nombre,
            ImagenPrincipalUrl = imagenes.FirstOrDefault()?.Url,
            Imagenes = imagenes,
            Activo = p.Activo
        };
    }
}

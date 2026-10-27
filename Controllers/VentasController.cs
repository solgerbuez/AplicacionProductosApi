using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppProductos.Common;
using AppProductos.Data;
using AppProductos.Dtos;
using AppProductos.Models;

namespace AppProductos.Controllers;

/// <summary>
/// Ventas = carrito confirmado. Flujo de la app:
/// 1) POST api/ventas con los ítems del carrito  -> venta "Pendiente" (el stock se descuenta/reserva)
/// 2) POST api/ventas/{id}/pagar                 -> pago simulado; si se aprueba la venta pasa a "Pagada"
/// 3) POST api/ventas/{id}/cancelar              -> devuelve el stock (solo si no está pagada, o Admin)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VentasController : ControllerBase
{
    private readonly AppDbContext _context;
    public VentasController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<PagedResult<VentaDto>>> GetAll(
        int page = 1, int pageSize = 10, int? idCliente = null, EstadoVenta? estado = null,
        DateTime? desde = null, DateTime? hasta = null)
    {
        var query = QueryCompleta().AsNoTracking();

        // Un cliente solo ve sus propias compras
        if (User.IsInRole(Roles.Cliente))
        {
            var miIdCliente = IdClienteLogueado();
            query = query.Where(v => v.IdCliente == miIdCliente);
        }
        else if (idCliente.HasValue)
        {
            query = query.Where(v => v.IdCliente == idCliente);
        }

        if (estado.HasValue) query = query.Where(v => v.Estado == estado);
        if (desde.HasValue) query = query.Where(v => v.Fecha >= desde.Value.Date);
        if (hasta.HasValue) query = query.Where(v => v.Fecha < hasta.Value.Date.AddDays(1));

        var total = await query.CountAsync();
        var ventas = await query
            .OrderByDescending(v => v.Fecha)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<VentaDto> { Items = ventas.Select(MapToDto).ToList(), Page = page, PageSize = pageSize, TotalCount = total });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VentaDto>> GetById(int id)
    {
        var venta = await QueryCompleta().AsNoTracking().FirstOrDefaultAsync(v => v.IdVenta == id);
        if (venta is null) return NotFound();
        if (User.IsInRole(Roles.Cliente) && venta.IdCliente != IdClienteLogueado()) return Forbid();

        return Ok(MapToDto(venta));
    }

    /// <summary>Confirma el carrito: crea la venta y descuenta stock.</summary>
    [HttpPost]
    public async Task<ActionResult<VentaDto>> Create(VentaCreateDto dto)
    {
        int idCliente;
        if (User.IsInRole(Roles.Cliente))
        {
            var propio = IdClienteLogueado();
            if (propio is null) return BadRequest("Tu usuario no tiene una ficha de cliente asociada.");
            idCliente = propio.Value;
        }
        else
        {
            if (dto.IdCliente is null) return BadRequest("Indicá el cliente de la venta.");
            if (!await _context.Clientes.AnyAsync(c => c.IdCliente == dto.IdCliente)) return BadRequest("El cliente indicado no existe.");
            idCliente = dto.IdCliente.Value;
        }

        // Si el mismo producto viene repetido en el carrito, se agrupa
        var items = dto.Items
            .GroupBy(i => i.IdProducto)
            .Select(g => new { IdProducto = g.Key, Cantidad = g.Sum(x => x.Cantidad) })
            .ToList();

        var ids = items.Select(i => i.IdProducto).ToList();

        await using var tx = await _context.Database.BeginTransactionAsync();

        var productos = await _context.Productos.Where(p => ids.Contains(p.IdProducto)).ToDictionaryAsync(p => p.IdProducto);

        var errores = new List<string>();
        foreach (var item in items)
        {
            if (!productos.TryGetValue(item.IdProducto, out var p) || !p.Activo)
                errores.Add($"El producto {item.IdProducto} no existe o no está disponible.");
            else if (p.Stock < item.Cantidad)
                errores.Add($"Stock insuficiente de '{p.Nombre}'. Disponible: {p.Stock}.");
        }
        if (errores.Count > 0) return BadRequest(new { errores });

        var venta = new Venta
        {
            Fecha = DateTime.Now,
            IdCliente = idCliente,
            IdUsuario = IdUsuarioLogueado(),
            Estado = EstadoVenta.Pendiente,
            Observaciones = dto.Observaciones
        };

        foreach (var item in items)
        {
            var p = productos[item.IdProducto];
            p.Stock -= item.Cantidad;
            venta.Detalles.Add(new DetalleVenta
            {
                IdProducto = p.IdProducto,
                Cantidad = item.Cantidad,
                PrecioUnitario = p.Precio   // el precio lo pone el servidor, nunca la app
            });
        }

        _context.Ventas.Add(venta);
        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        var creada = await QueryCompleta().AsNoTracking().FirstAsync(v => v.IdVenta == venta.IdVenta);
        return CreatedAtAction(nameof(GetById), new { id = venta.IdVenta }, MapToDto(creada));
    }

    /// <summary>Pago SIMULADO. Si el número de tarjeta termina en 0000 se rechaza (para probar).</summary>
    [HttpPost("{id}/pagar")]
    public async Task<ActionResult<PagoDto>> Pagar(int id, PagoCreateDto dto)
    {
        var venta = await _context.Ventas.Include(v => v.Detalles).FirstOrDefaultAsync(v => v.IdVenta == id);
        if (venta is null) return NotFound();
        if (User.IsInRole(Roles.Cliente) && venta.IdCliente != IdClienteLogueado()) return Forbid();
        if (venta.Estado != EstadoVenta.Pendiente) return BadRequest($"La venta está {venta.Estado}, no se puede pagar.");

        var metodo = await _context.MetodosPago.FirstOrDefaultAsync(m => m.IdMetodoPago == dto.IdMetodoPago && m.Activo);
        if (metodo is null) return BadRequest("Método de pago inválido.");

        var rechazado = !string.IsNullOrWhiteSpace(dto.NumeroTarjeta) && dto.NumeroTarjeta.Trim().EndsWith("0000");

        var pago = new Pago
        {
            IdVenta = venta.IdVenta,
            IdMetodoPago = metodo.IdMetodoPago,
            Monto = venta.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario),
            Fecha = DateTime.Now,
            Estado = rechazado ? EstadoPago.Rechazado : EstadoPago.Aprobado,
            CodigoTransaccion = $"SIM-{DateTime.Now:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}"
        };
        _context.Pagos.Add(pago);

        if (!rechazado) venta.Estado = EstadoVenta.Pagada;

        await _context.SaveChangesAsync();

        var resultado = new PagoDto
        {
            IdPago = pago.IdPago,
            Fecha = pago.Fecha,
            MetodoPago = metodo.Nombre,
            Monto = pago.Monto,
            Estado = pago.Estado.ToString(),
            CodigoTransaccion = pago.CodigoTransaccion
        };

        return rechazado ? UnprocessableEntity(resultado) : Ok(resultado);
    }

    /// <summary>Cancela la venta y devuelve el stock.</summary>
    [HttpPost("{id}/cancelar")]
    public async Task<IActionResult> Cancelar(int id)
    {
        var venta = await _context.Ventas.Include(v => v.Detalles).ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(v => v.IdVenta == id);
        if (venta is null) return NotFound();
        if (User.IsInRole(Roles.Cliente) && venta.IdCliente != IdClienteLogueado()) return Forbid();
        if (venta.Estado == EstadoVenta.Cancelada) return BadRequest("La venta ya está cancelada.");
        if (venta.Estado == EstadoVenta.Pagada && !User.IsInRole(Roles.Admin))
            return BadRequest("Solo un administrador puede cancelar una venta pagada.");

        foreach (var d in venta.Detalles) d.Producto!.Stock += d.Cantidad;
        venta.Estado = EstadoVenta.Cancelada;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [AllowAnonymous]
    [HttpGet("metodos-pago")]
    public async Task<ActionResult<List<MetodoPagoDto>>> GetMetodosPago() =>
        Ok(await _context.MetodosPago.AsNoTracking().Where(m => m.Activo).OrderBy(m => m.IdMetodoPago)
            .Select(m => new MetodoPagoDto { IdMetodoPago = m.IdMetodoPago, Nombre = m.Nombre }).ToListAsync());

    // ===================== Helpers =====================

    private IQueryable<Venta> QueryCompleta() => _context.Ventas
        .Include(v => v.Cliente)
        .Include(v => v.Usuario)
        .Include(v => v.Detalles).ThenInclude(d => d.Producto)
        .Include(v => v.Pagos).ThenInclude(p => p.MetodoPago)
        .AsSplitQuery();

    private int IdUsuarioLogueado() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private int? IdClienteLogueado() => int.TryParse(User.FindFirstValue("idCliente"), out var id) ? id : null;

    private static VentaDto MapToDto(Venta v) => new()
    {
        IdVenta = v.IdVenta,
        Fecha = v.Fecha,
        IdCliente = v.IdCliente,
        ClienteNombre = v.Cliente is null ? null : $"{v.Cliente.Nombre} {v.Cliente.Apellido}",
        UsuarioNombre = v.Usuario?.Username,
        Estado = v.Estado.ToString(),
        Observaciones = v.Observaciones,
        Total = v.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario),
        Detalles = v.Detalles.Select(d => new DetalleVentaDto
        {
            IdProducto = d.IdProducto,
            ProductoNombre = d.Producto?.Nombre,
            Cantidad = d.Cantidad,
            PrecioUnitario = d.PrecioUnitario
        }).ToList(),
        Pagos = v.Pagos.OrderBy(p => p.Fecha).Select(p => new PagoDto
        {
            IdPago = p.IdPago,
            Fecha = p.Fecha,
            MetodoPago = p.MetodoPago?.Nombre,
            Monto = p.Monto,
            Estado = p.Estado.ToString(),
            CodigoTransaccion = p.CodigoTransaccion
        }).ToList()
    };
}

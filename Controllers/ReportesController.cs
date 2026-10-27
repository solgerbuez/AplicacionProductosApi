using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppProductos.Common;
using AppProductos.Data;
using AppProductos.Dtos;
using AppProductos.Models;
using AppProductos.Services;

namespace AppProductos.Controllers;

/// <summary>
/// Reportes en PDF para el personal (Admin / Operador). Se pueden descargar (GET) o
/// enviar por mail como adjunto (POST api/reportes/{tipo}/enviar).
/// En los reportes con fechas, si no se indican se usa el mes actual; si se indica solo una, el mes de esa fecha.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Staff)]
public class ReportesController : ControllerBase
{
    private readonly ReporteService _reportes;
    private readonly IEmailService _email;
    private readonly AuditoriaService _auditoria;
    private readonly AppDbContext _context;

    public ReportesController(ReporteService reportes, IEmailService email, AuditoriaService auditoria, AppDbContext context)
    {
        _reportes = reportes;
        _email = email;
        _auditoria = auditoria;
        _context = context;
    }

    /// <summary>Productos activos con su stock, valor (stock × precio) y marca de stock bajo.</summary>
    [HttpGet("stock")]
    public Task<IActionResult> Stock(int? idCategoria = null) =>
        Descargar(ReporteTipos.Stock, new ReporteFiltros(IdCategoria: idCategoria));

    /// <summary>Productos activos con Stock menor o igual al stock mínimo.</summary>
    [HttpGet("stock-bajo")]
    public Task<IActionResult> StockBajo() =>
        Descargar(ReporteTipos.StockBajo, new ReporteFiltros());

    /// <summary>Ingresos de mercadería con sus detalles y el total del período.</summary>
    [HttpGet("ingresos")]
    public Task<IActionResult> Ingresos(DateTime? desde = null, DateTime? hasta = null, int? idProveedor = null) =>
        Descargar(ReporteTipos.Ingresos, new ReporteFiltros(Desde: desde, Hasta: hasta, IdProveedor: idProveedor));

    /// <summary>Ventas con cliente, estado, total y total general del período.</summary>
    [HttpGet("ventas")]
    public Task<IActionResult> Ventas(DateTime? desde = null, DateTime? hasta = null, EstadoVenta? estado = null) =>
        Descargar(ReporteTipos.Ventas, new ReporteFiltros(Desde: desde, Hasta: hasta, Estado: estado));

    /// <summary>
    /// Genera el reporte (tipo = stock | stock-bajo | ingresos | ventas) y lo envía por mail como PDF adjunto.
    /// Si el servidor SMTP falla responde 502 y el error queda en LogErrores.
    /// </summary>
    [HttpPost("{tipo}/enviar")]
    public async Task<IActionResult> Enviar(string tipo, EnviarReporteDto dto)
    {
        tipo = tipo.ToLowerInvariant();
        var filtros = new ReporteFiltros(dto.Desde, dto.Hasta, dto.IdCategoria, dto.IdProveedor, dto.Estado);

        var (reporte, error) = await PrepararAsync(tipo, filtros);
        if (error is not null) return error;

        var asunto = $"Reporte: {reporte!.Titulo}";
        var cuerpo =
            $"Hola,{Environment.NewLine}{Environment.NewLine}" +
            $"Te enviamos adjunto el reporte \"{reporte.Titulo}\" generado el {DateTime.Now:dd/MM/yyyy HH:mm}.{Environment.NewLine}{Environment.NewLine}" +
            "Este mensaje fue generado automáticamente.";

        try
        {
            await _email.EnviarConAdjuntoAsync(dto.Email, asunto, cuerpo, reporte.NombreArchivo, reporte.Contenido);
        }
        catch (EmailException ex)
        {
            var idLog = await RegistrarErrorAsync(ex, tipo);

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status502BadGateway,
                Title = "No se pudo enviar el reporte por mail.",
                Detail = ex.Message,
                Instance = Request.Path
            };
            if (idLog.HasValue) problem.Extensions["idLogError"] = idLog.Value;
            return StatusCode(StatusCodes.Status502BadGateway, problem);
        }

        await _auditoria.RegistrarAsync("EnvioReporte", "Reporte", tipo, detalle: dto.Email, idUsuario: IdUsuarioLogueado());
        return Ok(new { mensaje = $"El reporte se envió a {dto.Email}." });
    }

    // ================== AUXILIARES ==================

    private async Task<IActionResult> Descargar(string tipo, ReporteFiltros filtros)
    {
        var (reporte, error) = await PrepararAsync(tipo, filtros);
        if (error is not null) return error;
        return File(reporte!.Contenido, "application/pdf", reporte.NombreArchivo);
    }

    /// <summary>Valida tipo y filtros y genera el PDF. Devuelve el reporte o el error (400) a responder.</summary>
    private async Task<(ReporteArchivo? Reporte, IActionResult? Error)> PrepararAsync(string tipo, ReporteFiltros filtros)
    {
        if (!ReporteTipos.Todos.Contains(tipo))
            return (null, BadRequest($"Tipo de reporte inválido. Valores posibles: {string.Join(", ", ReporteTipos.Todos)}."));

        if (tipo is ReporteTipos.Ingresos or ReporteTipos.Ventas)
        {
            var (desde, hasta) = ReporteService.ResolverRango(filtros.Desde, filtros.Hasta);
            if (desde > hasta)
                return (null, BadRequest("La fecha 'desde' no puede ser posterior a la fecha 'hasta'."));
        }

        if (filtros.IdCategoria.HasValue && (tipo == ReporteTipos.Stock || tipo == ReporteTipos.StockBajo) &&
            !await _context.Categorias.AnyAsync(c => c.IdCategoria == filtros.IdCategoria))
            return (null, BadRequest("La categoría indicada no existe."));

        if (filtros.IdProveedor.HasValue && tipo == ReporteTipos.Ingresos &&
            !await _context.Proveedores.AnyAsync(p => p.IdProveedor == filtros.IdProveedor))
            return (null, BadRequest("El proveedor indicado no existe."));

        var reporte = await _reportes.GenerarAsync(tipo, filtros);
        return (reporte, null);
    }

    /// <summary>Guarda en LogErrores la falla de envío (origen API). Nunca debe romper la respuesta.</summary>
    private async Task<int?> RegistrarErrorAsync(Exception ex, string tipo)
    {
        try
        {
            var mensaje = $"Falló el envío por mail del reporte '{tipo}': {ex.Message}";
            var log = new LogError
            {
                Fecha = DateTime.Now,
                Mensaje = mensaje.Length <= 2000 ? mensaje : mensaje[..2000],
                Excepcion = ex.ToString(),
                Ruta = Request.Path.Value,
                MetodoHttp = Request.Method,
                IdUsuario = IdUsuarioLogueado(),
                Origen = OrigenLog.Api
            };
            _context.LogErrores.Add(log);
            await _context.SaveChangesAsync();
            return log.IdLogError;
        }
        catch
        {
            return null;
        }
    }

    private int? IdUsuarioLogueado() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

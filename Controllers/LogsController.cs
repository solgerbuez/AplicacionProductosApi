using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppProductos.Common;
using AppProductos.Data;
using AppProductos.Dtos;
using AppProductos.Models;

namespace AppProductos.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly AppDbContext _context;
    public LogsController(AppDbContext context) => _context = context;

    /// <summary>
    /// La app móvil informa un error. Es anónimo a propósito (para poder registrar fallas del login);
    /// si la app manda el token, el error queda asociado a ese usuario.
    /// </summary>
    [HttpPost("app")]
    [AllowAnonymous]
    public async Task<IActionResult> RegistrarErrorApp(AppLogDto dto)
    {
        // Plataforma y versión no tienen columna propia: se agregan al final del detalle
        var contexto = $"[Plataforma: {dto.Plataforma ?? "-"} | Versión de la app: {dto.VersionApp ?? "-"}]";
        var detalle = string.IsNullOrWhiteSpace(dto.Detalle) ? contexto : dto.Detalle + Environment.NewLine + Environment.NewLine + contexto;

        _context.LogErrores.Add(new LogError
        {
            Fecha = DateTime.Now,
            Mensaje = dto.Mensaje,
            Excepcion = detalle,
            Ruta = dto.Pantalla,
            IdUsuario = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null,
            Origen = OrigenLog.App
        });
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

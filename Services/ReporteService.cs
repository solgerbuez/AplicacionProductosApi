using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using AppProductos.Data;
using AppProductos.Dtos;
using AppProductos.Models;

namespace AppProductos.Services;

/// <summary>
/// Genera los reportes en PDF (QuestPDF). Cada método devuelve el archivo en memoria (byte[]),
/// así el mismo reporte sirve para descargarlo o para adjuntarlo a un mail.
/// </summary>
public class ReporteService
{
    private static readonly CultureInfo Cultura = ObtenerCultura();

    private readonly AppDbContext _context;
    private readonly string _negocio;

    public ReporteService(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _negocio = config["Reportes:NombreNegocio"] ?? "AppProductos";
    }

    // ================== API PÚBLICA ==================

    public Task<ReporteArchivo> GenerarAsync(string tipo, ReporteFiltros filtros) => tipo switch
    {
        ReporteTipos.Stock => GenerarStockAsync(filtros.IdCategoria, soloBajo: false),
        ReporteTipos.StockBajo => GenerarStockAsync(filtros.IdCategoria, soloBajo: true),
        ReporteTipos.Ingresos => GenerarIngresosAsync(filtros),
        ReporteTipos.Ventas => GenerarVentasAsync(filtros),
        _ => throw new ArgumentException($"Tipo de reporte desconocido: {tipo}", nameof(tipo))
    };

    /// <summary>
    /// Completa el rango de fechas. Sin fechas = mes actual. Si falta solo una, se usa el mes de la otra.
    /// </summary>
    public static (DateTime Desde, DateTime Hasta) ResolverRango(DateTime? desde, DateTime? hasta)
    {
        var refDesde = (desde ?? hasta ?? DateTime.Today).Date;
        var refHasta = (hasta ?? desde ?? DateTime.Today).Date;
        var d = desde?.Date ?? new DateTime(refDesde.Year, refDesde.Month, 1);
        var h = hasta?.Date ?? new DateTime(refHasta.Year, refHasta.Month, 1).AddMonths(1).AddDays(-1);
        return (d, h);
    }

    // ================== STOCK / STOCK BAJO ==================

    private record FilaStock(string Producto, string Categoria, int Stock, int StockMinimo, decimal Precio)
    {
        public bool Bajo => Stock <= StockMinimo;
        public decimal Valor => Stock * Precio;
    }

    private async Task<ReporteArchivo> GenerarStockAsync(int? idCategoria, bool soloBajo)
    {
        var query = _context.Productos.AsNoTracking().Where(p => p.Activo);
        var filtros = new List<string>();

        if (idCategoria.HasValue)
        {
            var id = idCategoria.Value;
            query = query.Where(p => p.IdCategoria == id);
            var nombre = await _context.Categorias.AsNoTracking()
                .Where(c => c.IdCategoria == id).Select(c => c.Nombre).FirstOrDefaultAsync();
            filtros.Add($"Categoría: {nombre ?? id.ToString()}");
        }
        else
        {
            filtros.Add("Categoría: todas");
        }

        if (soloBajo)
        {
            query = query.Where(p => p.Stock <= p.StockMinimo);
            filtros.Add("Productos activos con stock menor o igual al stock mínimo");
        }
        else
        {
            filtros.Add("Solo productos activos");
        }

        var datos = await query
            .OrderBy(p => p.Categoria!.Nombre).ThenBy(p => p.Nombre)
            .Select(p => new { p.Nombre, Categoria = p.Categoria!.Nombre, p.Stock, p.StockMinimo, p.Precio })
            .ToListAsync();

        var filas = datos
            .Select(d => new FilaStock(d.Nombre, d.Categoria, d.Stock, d.StockMinimo, d.Precio))
            .ToList();

        var titulo = soloBajo ? "Reporte de productos con stock bajo" : "Reporte de stock";

        var pdf = Armar(titulo, filtros, contenedor =>
        {
            contenedor.Column(col =>
            {
                if (filas.Count == 0)
                {
                    col.Item().Text("No hay productos para los filtros indicados.");
                    return;
                }

                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(4);
                        c.RelativeColumn(2.5f);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(1.6f);
                    });

                    t.Header(h =>
                    {
                        Cab(h, "Producto");
                        Cab(h, "Categoría");
                        Cab(h, "Stock", derecha: true);
                        Cab(h, "Mínimo", derecha: true);
                        Cab(h, "Precio", derecha: true);
                        Cab(h, "Valor", derecha: true);
                        Cab(h, "Estado");
                    });

                    foreach (var f in filas)
                    {
                        Dato(t, f.Producto, alerta: f.Bajo);
                        Dato(t, f.Categoria, alerta: f.Bajo);
                        Dato(t, f.Stock.ToString(Cultura), derecha: true, alerta: f.Bajo);
                        Dato(t, f.StockMinimo.ToString(Cultura), derecha: true, alerta: f.Bajo);
                        Dato(t, Dinero(f.Precio), derecha: true, alerta: f.Bajo);
                        Dato(t, Dinero(f.Valor), derecha: true, alerta: f.Bajo);
                        Dato(t, f.Bajo ? "STOCK BAJO" : "OK", alerta: f.Bajo, negrita: f.Bajo);
                    }
                });

                col.Item().PaddingTop(10).AlignRight().Width(260).Column(tot =>
                {
                    Total(tot, "Productos listados", filas.Count.ToString(Cultura));
                    Total(tot, "Productos con stock bajo", filas.Count(f => f.Bajo).ToString(Cultura));
                    Total(tot, "Valor total del stock", Dinero(filas.Sum(f => f.Valor)), destacado: true);
                });
            });
        });

        return new ReporteArchivo(pdf, NombreArchivo(soloBajo ? ReporteTipos.StockBajo : ReporteTipos.Stock), titulo);
    }

    // ================== INGRESOS ==================

    private async Task<ReporteArchivo> GenerarIngresosAsync(ReporteFiltros f)
    {
        var (desde, hasta) = ResolverRango(f.Desde, f.Hasta);
        var hastaExclusivo = hasta.AddDays(1);

        var query = _context.Ingresos.AsNoTracking()
            .Include(i => i.Proveedor)
            .Include(i => i.Detalles).ThenInclude(d => d.Producto)
            .AsSplitQuery()
            .Where(i => i.Fecha >= desde && i.Fecha < hastaExclusivo);

        var filtros = new List<string> { $"Período: {Dia(desde)} al {Dia(hasta)}" };

        if (f.IdProveedor.HasValue)
        {
            var id = f.IdProveedor.Value;
            query = query.Where(i => i.IdProveedor == id);
            var nombre = await _context.Proveedores.AsNoTracking()
                .Where(p => p.IdProveedor == id).Select(p => p.RazonSocial).FirstOrDefaultAsync();
            filtros.Add($"Proveedor: {nombre ?? id.ToString()}");
        }
        else
        {
            filtros.Add("Proveedor: todos");
        }

        var ingresos = await query.OrderBy(i => i.Fecha).ToListAsync();
        const string titulo = "Reporte de ingresos de mercadería";

        var pdf = Armar(titulo, filtros, contenedor =>
        {
            contenedor.Column(col =>
            {
                if (ingresos.Count == 0)
                {
                    col.Item().Text("No hay ingresos en el período indicado.");
                    return;
                }

                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(5);
                        c.RelativeColumn(1.5f);
                        c.RelativeColumn(2.5f);
                        c.RelativeColumn(2.5f);
                    });

                    t.Header(h =>
                    {
                        Cab(h, "Producto");
                        Cab(h, "Cantidad", derecha: true);
                        Cab(h, "Costo unitario", derecha: true);
                        Cab(h, "Subtotal", derecha: true);
                    });

                    foreach (var ing in ingresos)
                    {
                        t.Cell().ColumnSpan(4).Background(Colors.Grey.Lighten3).Padding(4).Text(x =>
                        {
                            x.Span($"Ingreso #{ing.IdIngreso}").Bold();
                            x.Span($"  ·  {Fecha(ing.Fecha)}  ·  {ing.Proveedor?.RazonSocial ?? "-"}");
                            if (!string.IsNullOrWhiteSpace(ing.NumeroComprobante))
                                x.Span($"  ·  Comprobante {ing.NumeroComprobante}");
                        });

                        foreach (var d in ing.Detalles)
                        {
                            Dato(t, d.Producto?.Nombre ?? "-");
                            Dato(t, d.Cantidad.ToString(Cultura), derecha: true);
                            Dato(t, Dinero(d.CostoUnitario), derecha: true);
                            Dato(t, Dinero(d.Cantidad * d.CostoUnitario), derecha: true);
                        }

                        Dato(t, "Total del ingreso", derecha: true, negrita: true, span: 3);
                        Dato(t, Dinero(TotalIngreso(ing)), derecha: true, negrita: true);
                    }
                });

                col.Item().PaddingTop(10).AlignRight().Width(260).Column(tot =>
                {
                    Total(tot, "Cantidad de ingresos", ingresos.Count.ToString(Cultura));
                    Total(tot, "Total del período", Dinero(ingresos.Sum(TotalIngreso)), destacado: true);
                });
            });
        });

        return new ReporteArchivo(pdf, NombreArchivo(ReporteTipos.Ingresos), titulo);
    }

    private static decimal TotalIngreso(Ingreso i) => i.Detalles.Sum(d => d.Cantidad * d.CostoUnitario);

    // ================== VENTAS ==================

    private record FilaVenta(int IdVenta, DateTime Fecha, string Cliente, EstadoVenta Estado, int Items, decimal Total);

    private async Task<ReporteArchivo> GenerarVentasAsync(ReporteFiltros f)
    {
        var (desde, hasta) = ResolverRango(f.Desde, f.Hasta);
        var hastaExclusivo = hasta.AddDays(1);

        var query = _context.Ventas.AsNoTracking()
            .Where(v => v.Fecha >= desde && v.Fecha < hastaExclusivo);

        var filtros = new List<string> { $"Período: {Dia(desde)} al {Dia(hasta)}" };

        if (f.Estado.HasValue)
        {
            var estado = f.Estado.Value;
            query = query.Where(v => v.Estado == estado);
            filtros.Add($"Estado: {estado}");
        }
        else
        {
            filtros.Add("Estado: todos");
        }

        var datos = await query
            .OrderBy(v => v.Fecha)
            .Select(v => new
            {
                v.IdVenta,
                v.Fecha,
                Apellido = v.Cliente!.Apellido,
                Nombre = v.Cliente!.Nombre,
                v.Estado,
                Items = v.Detalles.Sum(d => d.Cantidad),
                Total = v.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario)
            })
            .ToListAsync();

        var filas = datos
            .Select(d => new FilaVenta(d.IdVenta, d.Fecha, $"{d.Apellido}, {d.Nombre}", d.Estado, d.Items, d.Total))
            .ToList();

        const string titulo = "Reporte de ventas";

        var pdf = Armar(titulo, filtros, contenedor =>
        {
            contenedor.Column(col =>
            {
                if (filas.Count == 0)
                {
                    col.Item().Text("No hay ventas para los filtros indicados.");
                    return;
                }

                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(2.6f);
                        c.RelativeColumn(4);
                        c.RelativeColumn(2);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(2.6f);
                    });

                    t.Header(h =>
                    {
                        Cab(h, "N°");
                        Cab(h, "Fecha");
                        Cab(h, "Cliente");
                        Cab(h, "Estado");
                        Cab(h, "Ítems", derecha: true);
                        Cab(h, "Total", derecha: true);
                    });

                    foreach (var v in filas)
                    {
                        var cancelada = v.Estado == EstadoVenta.Cancelada;
                        Dato(t, v.IdVenta.ToString(Cultura), alerta: cancelada);
                        Dato(t, Fecha(v.Fecha), alerta: cancelada);
                        Dato(t, v.Cliente, alerta: cancelada);
                        Dato(t, v.Estado.ToString(), alerta: cancelada);
                        Dato(t, v.Items.ToString(Cultura), derecha: true, alerta: cancelada);
                        Dato(t, Dinero(v.Total), derecha: true, alerta: cancelada);
                    }
                });

                col.Item().PaddingTop(10).AlignRight().Width(260).Column(tot =>
                {
                    Total(tot, "Cantidad de ventas", filas.Count.ToString(Cultura));
                    foreach (var grupo in filas.GroupBy(v => v.Estado).OrderBy(g => g.Key))
                        Total(tot, $"Subtotal {grupo.Key} ({grupo.Count()})", Dinero(grupo.Sum(v => v.Total)));
                    Total(tot, "Total general del período", Dinero(filas.Sum(v => v.Total)), destacado: true);
                });
            });
        });

        return new ReporteArchivo(pdf, NombreArchivo(ReporteTipos.Ventas), titulo);
    }

    // ================== ARMADO COMÚN DEL PDF ==================

    private byte[] Armar(string titulo, IEnumerable<string> filtros, Action<IContainer> contenido)
    {
        var emision = DateTime.Now;
        var lineasFiltros = filtros.ToList();

        return Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(30);
                pagina.DefaultTextStyle(x => x.FontSize(9));

                pagina.Header().Element(c => Encabezado(c, titulo, emision, lineasFiltros));
                pagina.Content().PaddingVertical(10).Element(contenido);

                pagina.Footer().Row(fila =>
                {
                    fila.RelativeItem().Text(_negocio).FontSize(8).FontColor(Colors.Grey.Darken1);
                    fila.RelativeItem().AlignRight().Text(texto =>
                    {
                        texto.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
                        texto.Span("Página ");
                        texto.CurrentPageNumber();
                        texto.Span(" de ");
                        texto.TotalPages();
                    });
                });
            });
        }).GeneratePdf();
    }

    private void Encabezado(IContainer contenedor, string titulo, DateTime emision, List<string> filtros)
    {
        contenedor.PaddingBottom(8).BorderBottom(1).BorderColor(Colors.Grey.Medium).Column(col =>
        {
            col.Item().Row(fila =>
            {
                fila.RelativeItem().Text(_negocio).FontSize(16).Bold().FontColor(Colors.Blue.Darken3);
                fila.ConstantItem(160).AlignRight().Text($"Emitido: {Fecha(emision)}").FontSize(8);
            });
            col.Item().PaddingTop(2).Text(titulo).FontSize(12).SemiBold();
            foreach (var filtro in filtros)
                col.Item().Text(filtro).FontSize(8).FontColor(Colors.Grey.Darken2);
        });
    }

    // ---- estilos de tabla ----

    private static IContainer CeldaCab(IContainer c) =>
        c.Background(Colors.Blue.Darken3).Padding(4)
         .DefaultTextStyle(x => x.FontColor(Colors.White).SemiBold());

    private static IContainer Celda(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(4);

    private static void Cab(TableCellDescriptor h, string texto, bool derecha = false)
    {
        IContainer celda = h.Cell().Element(CeldaCab);
        if (derecha) celda = celda.AlignRight();
        celda.Text(texto);
    }

    private static void Dato(TableDescriptor t, string texto, bool derecha = false, bool alerta = false,
        bool negrita = false, uint span = 1)
    {
        var contenedor = t.Cell();
        if (span > 1) contenedor = contenedor.ColumnSpan(span);

        IContainer celda = contenedor.Element(Celda);
        if (derecha) celda = celda.AlignRight();

        var txt = celda.Text(texto);
        if (negrita) txt.Bold();
        if (alerta) txt.FontColor(Colors.Red.Darken2);
    }

    private static void Total(ColumnDescriptor col, string etiqueta, string valor, bool destacado = false)
    {
        col.Item().PaddingVertical(1).Row(fila =>
        {
            var izq = fila.RelativeItem().Text(etiqueta);
            var der = fila.AutoItem().Text(valor);
            if (destacado)
            {
                izq.Bold();
                der.Bold();
            }
        });
    }

    // ---- formatos ----

    private static string Dinero(decimal monto) => "$ " + monto.ToString("N2", Cultura);
    private static string Fecha(DateTime fecha) => fecha.ToString("dd/MM/yyyy HH:mm", Cultura);
    private static string Dia(DateTime fecha) => fecha.ToString("dd/MM/yyyy", Cultura);
    private static string NombreArchivo(string tipo) => $"reporte-{tipo}-{DateTime.Now:yyyyMMdd-HHmm}.pdf";

    private static CultureInfo ObtenerCultura()
    {
        try { return CultureInfo.GetCultureInfo("es-AR"); }
        catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
    }
}

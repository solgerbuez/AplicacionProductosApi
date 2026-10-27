using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using AppProductos.Common;
using AppProductos.Models;

namespace AppProductos.Data;

public class AppDbContext : DbContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor httpContextAccessor)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<ProductoImagen> ProductoImagenes => Set<ProductoImagen>();
    public DbSet<MetodoPago> MetodosPago => Set<MetodoPago>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<DetalleVenta> DetallesVenta => Set<DetalleVenta>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<Ingreso> Ingresos => Set<Ingreso>();
    public DbSet<DetalleIngreso> DetallesIngreso => Set<DetalleIngreso>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();
    public DbSet<LogError> LogErrores => Set<LogError>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ---------- Seguridad ----------
        modelBuilder.Entity<Rol>(e =>
        {
            e.HasKey(r => r.IdRol);
            e.Property(r => r.Nombre).HasMaxLength(30).IsRequired();
            e.HasIndex(r => r.Nombre).IsUnique();
        });

        modelBuilder.Entity<Usuario>(e =>
        {
            e.HasKey(u => u.IdUsuario);
            e.Property(u => u.Username).HasMaxLength(50).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(100).IsRequired();
            e.Property(u => u.Email).HasMaxLength(150);
            e.HasIndex(u => u.Username).IsUnique();
            e.HasOne(u => u.Rol).WithMany(r => r.Usuarios).HasForeignKey(u => u.IdRol);
        });

        // ---------- Personas ----------
        modelBuilder.Entity<Cliente>(e =>
        {
            e.HasKey(c => c.IdCliente);
            e.Property(c => c.Nombre).HasMaxLength(80).IsRequired();
            e.Property(c => c.Apellido).HasMaxLength(80).IsRequired();
            e.Property(c => c.Dni).HasMaxLength(15);
            e.Property(c => c.Email).HasMaxLength(150);
            e.Property(c => c.Telefono).HasMaxLength(30);
            e.Property(c => c.Direccion).HasMaxLength(200);
            e.HasIndex(c => c.Dni).IsUnique();
            e.HasIndex(c => c.IdUsuario).IsUnique();   // 1 usuario <-> 1 cliente
            e.HasOne(c => c.Usuario).WithOne(u => u.Cliente).HasForeignKey<Cliente>(c => c.IdUsuario);
        });

        modelBuilder.Entity<Proveedor>(e =>
        {
            e.HasKey(p => p.IdProveedor);
            e.Property(p => p.RazonSocial).HasMaxLength(150).IsRequired();
            e.Property(p => p.Cuit).HasMaxLength(13);
            e.Property(p => p.Telefono).HasMaxLength(30);
            e.Property(p => p.Email).HasMaxLength(150);
            e.Property(p => p.Direccion).HasMaxLength(200);
            e.HasIndex(p => p.Cuit).IsUnique();
        });

        // ---------- Catálogo ----------
        modelBuilder.Entity<Categoria>(e =>
        {
            e.HasKey(c => c.IdCategoria);
            e.Property(c => c.Nombre).HasMaxLength(80).IsRequired();
            e.Property(c => c.Descripcion).HasMaxLength(300);
            e.HasIndex(c => c.Nombre).IsUnique();
        });

        modelBuilder.Entity<Producto>(e =>
        {
            e.HasKey(p => p.IdProducto);
            e.Property(p => p.Nombre).HasMaxLength(150).IsRequired();
            e.Property(p => p.Descripcion).HasMaxLength(1000);
            e.Property(p => p.Precio).HasPrecision(12, 2);
            e.HasOne(p => p.Categoria).WithMany(c => c.Productos).HasForeignKey(p => p.IdCategoria);
        });

        modelBuilder.Entity<ProductoImagen>(e =>
        {
            e.HasKey(i => i.IdProductoImagen);
            e.Property(i => i.NombreArchivo).HasMaxLength(100).IsRequired();
            e.HasOne(i => i.Producto).WithMany(p => p.Imagenes)
             .HasForeignKey(i => i.IdProducto).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Ventas ----------
        modelBuilder.Entity<MetodoPago>(e =>
        {
            e.HasKey(m => m.IdMetodoPago);
            e.Property(m => m.Nombre).HasMaxLength(50).IsRequired();
            e.HasIndex(m => m.Nombre).IsUnique();
        });

        modelBuilder.Entity<Venta>(e =>
        {
            e.HasKey(v => v.IdVenta);
            e.Property(v => v.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(v => v.Observaciones).HasMaxLength(500);
            e.HasOne(v => v.Cliente).WithMany(c => c.Ventas).HasForeignKey(v => v.IdCliente);
            e.HasOne(v => v.Usuario).WithMany().HasForeignKey(v => v.IdUsuario);
        });

        modelBuilder.Entity<DetalleVenta>(e =>
        {
            e.HasKey(d => d.IdDetalleVenta);
            e.Property(d => d.PrecioUnitario).HasPrecision(12, 2);
            e.HasIndex(d => new { d.IdVenta, d.IdProducto }).IsUnique();
            e.HasOne(d => d.Venta).WithMany(v => v.Detalles)
             .HasForeignKey(d => d.IdVenta).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.Producto).WithMany(p => p.DetallesVenta).HasForeignKey(d => d.IdProducto);
        });

        modelBuilder.Entity<Pago>(e =>
        {
            e.HasKey(p => p.IdPago);
            e.Property(p => p.Monto).HasPrecision(12, 2);
            e.Property(p => p.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.CodigoTransaccion).HasMaxLength(50);
            e.HasOne(p => p.Venta).WithMany(v => v.Pagos)
             .HasForeignKey(p => p.IdVenta).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.MetodoPago).WithMany(m => m.Pagos).HasForeignKey(p => p.IdMetodoPago);
        });

        // ---------- Ingresos de mercadería ----------
        modelBuilder.Entity<Ingreso>(e =>
        {
            e.HasKey(i => i.IdIngreso);
            e.Property(i => i.NumeroComprobante).HasMaxLength(30);
            e.Property(i => i.Observaciones).HasMaxLength(500);
            e.HasOne(i => i.Proveedor).WithMany(p => p.Ingresos).HasForeignKey(i => i.IdProveedor);
            e.HasOne(i => i.Usuario).WithMany().HasForeignKey(i => i.IdUsuario);
        });

        modelBuilder.Entity<DetalleIngreso>(e =>
        {
            e.HasKey(d => d.IdDetalleIngreso);
            e.Property(d => d.CostoUnitario).HasPrecision(12, 2);
            e.HasIndex(d => new { d.IdIngreso, d.IdProducto }).IsUnique();
            e.HasOne(d => d.Ingreso).WithMany(i => i.Detalles)
             .HasForeignKey(d => d.IdIngreso).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.Producto).WithMany(p => p.DetallesIngreso).HasForeignKey(d => d.IdProducto);
        });

        // ---------- Auditoría y errores ----------
        modelBuilder.Entity<Auditoria>(e =>
        {
            e.HasKey(a => a.IdAuditoria);
            e.Property(a => a.Accion).HasMaxLength(30).IsRequired();
            e.Property(a => a.Entidad).HasMaxLength(50).IsRequired();
            e.Property(a => a.IdEntidad).HasMaxLength(50);
            e.Property(a => a.Ip).HasMaxLength(45);
            e.HasIndex(a => a.Fecha);
        });

        modelBuilder.Entity<LogError>(e =>
        {
            e.HasKey(l => l.IdLogError);
            e.Property(l => l.Mensaje).HasMaxLength(2000).IsRequired();
            e.Property(l => l.Ruta).HasMaxLength(300);
            e.Property(l => l.MetodoHttp).HasMaxLength(10);
            e.Property(l => l.Origen).HasMaxLength(10).IsRequired().HasDefaultValue(OrigenLog.Api);
            e.HasIndex(l => l.Fecha);
        });

        // Por defecto nada se borra en cascada (evita borrar ventas/ingresos sin querer
        // y el error de "multiple cascade paths" de SQL Server). Solo los detalles,
        // pagos e imágenes se borran junto con su cabecera.
        foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
        {
            if (!EsCascadaPermitida(fk.DeclaringEntityType.ClrType, fk.PrincipalEntityType.ClrType))
                fk.DeleteBehavior = DeleteBehavior.Restrict;
        }

        SeedData(modelBuilder);
    }

    private static bool EsCascadaPermitida(Type dependiente, Type principal) =>
        (dependiente == typeof(DetalleVenta) && principal == typeof(Venta)) ||
        (dependiente == typeof(Pago) && principal == typeof(Venta)) ||
        (dependiente == typeof(DetalleIngreso) && principal == typeof(Ingreso)) ||
        (dependiente == typeof(ProductoImagen) && principal == typeof(Producto));

    private static void SeedData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Rol>().HasData(
            new Rol { IdRol = Common.Roles.IdAdmin, Nombre = Common.Roles.Admin },
            new Rol { IdRol = Common.Roles.IdOperador, Nombre = Common.Roles.Operador },
            new Rol { IdRol = Common.Roles.IdCliente, Nombre = Common.Roles.Cliente });

        modelBuilder.Entity<MetodoPago>().HasData(
            new MetodoPago { IdMetodoPago = 1, Nombre = "Efectivo" },
            new MetodoPago { IdMetodoPago = 2, Nombre = "Tarjeta de débito" },
            new MetodoPago { IdMetodoPago = 3, Nombre = "Tarjeta de crédito" },
            new MetodoPago { IdMetodoPago = 4, Nombre = "Mercado Pago" },
            new MetodoPago { IdMetodoPago = 5, Nombre = "Transferencia" });

        // Usuario administrador inicial (cambiar la contraseña después del primer login)
        modelBuilder.Entity<Usuario>().HasData(new Usuario
        {
            IdUsuario = 1,
            Username = "admin",
            PasswordHash = "$2a$11$d7bnqBUBuAGSbzBF0KMKuuz9/ANFE8hsF6xAoz68Sc/qx.m1sPssC",
            Email = null,
            IdRol = Common.Roles.IdAdmin,
            Activo = true,
            FechaAlta = new DateTime(2026, 1, 1)
        });
    }

    // ================== AUDITORÍA AUTOMÁTICA ==================
    // Cada Alta / Modificación / Baja que pase por SaveChanges queda registrada
    // en la tabla Auditorias con el usuario logueado y los valores cambiados.

    private static readonly HashSet<string> CamposOcultos = new() { nameof(Usuario.PasswordHash) };

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var cambios = ChangeTracker.Entries()
            .Where(e => e.Entity is not Auditoria && e.Entity is not LogError &&
                        e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => new CambioPendiente(e, e.State, ArmarDetalle(e)))
            .ToList();

        var resultado = await base.SaveChangesAsync(cancellationToken);

        if (cambios.Count > 0)
        {
            var http = _httpContextAccessor.HttpContext;
            var idUsuarioStr = http?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? idUsuario = int.TryParse(idUsuarioStr, out var id) ? id : null;
            var ip = http?.Connection.RemoteIpAddress?.ToString();

            foreach (var c in cambios)
            {
                Auditorias.Add(new Auditoria
                {
                    Fecha = DateTime.Now,
                    IdUsuario = idUsuario,
                    Accion = c.Estado switch
                    {
                        EntityState.Added => "Alta",
                        EntityState.Deleted => "Baja",
                        _ => "Modificacion"
                    },
                    Entidad = c.Entry.Metadata.ClrType.Name,
                    // Para las altas la clave real recién existe después de guardar
                    IdEntidad = ObtenerClave(c.Entry),
                    Detalle = c.Detalle,
                    Ip = ip
                });
            }

            await base.SaveChangesAsync(cancellationToken);
        }

        return resultado;
    }

    private static string? ObtenerClave(EntityEntry entry)
    {
        var pk = entry.Metadata.FindPrimaryKey();
        if (pk is null) return null;
        return string.Join(",", pk.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString()));
    }

    private static string? ArmarDetalle(EntityEntry entry)
    {
        var datos = new Dictionary<string, object?>();

        foreach (var prop in entry.Properties)
        {
            var nombre = prop.Metadata.Name;
            if (CamposOcultos.Contains(nombre)) continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    if (!prop.IsTemporary) datos[nombre] = prop.CurrentValue;
                    break;
                case EntityState.Deleted:
                    datos[nombre] = prop.OriginalValue;
                    break;
                case EntityState.Modified when prop.IsModified && !Equals(prop.OriginalValue, prop.CurrentValue):
                    datos[nombre] = new { antes = prop.OriginalValue, despues = prop.CurrentValue };
                    break;
            }
        }

        return datos.Count == 0 ? null : JsonSerializer.Serialize(datos);
    }

    private record CambioPendiente(EntityEntry Entry, EntityState Estado, string? Detalle);
}

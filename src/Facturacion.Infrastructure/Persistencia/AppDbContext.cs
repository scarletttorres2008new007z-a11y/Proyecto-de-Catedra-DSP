using Facturacion.Application.Abstracciones;
using Facturacion.Domain;
using Facturacion.Domain.Entidades;
using Facturacion.Infrastructure.Identidad;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Infrastructure.Persistencia;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<UsuarioAplicacion>(options), IAppDbContext
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<DocumentoTributario> Documentos => Set<DocumentoTributario>();
    public DbSet<DetalleDocumento> DetallesDocumento => Set<DetalleDocumento>();
    public DbSet<ArchivoGenerado> ArchivosGenerados => Set<ArchivoGenerado>();
    public DbSet<EnvioCorreo> EnviosCorreo => Set<EnvioCorreo>();
    public DbSet<Anulacion> Anulaciones => Set<Anulacion>();
    public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();
    public DbSet<Respaldo> Respaldos => Set<Respaldo>();
    public DbSet<Correlativo> Correlativos => Set<Correlativo>();
    public DbSet<ConfiguracionEmisor> ConfiguracionEmisor => Set<ConfiguracionEmisor>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<UsuarioAplicacion>(e => e.Property(u => u.NombreCompleto).HasMaxLength(150));

        b.Entity<Cliente>(e =>
        {
            e.Property(c => c.Nombre).HasMaxLength(250).IsRequired();
            e.Property(c => c.NombreComercial).HasMaxLength(150);
            e.Property(c => c.TipoDocumento).HasMaxLength(2);
            e.Property(c => c.NumeroDocumento).HasMaxLength(20);
            e.Property(c => c.Nrc).HasMaxLength(8);
            e.Property(c => c.CodActividad).HasMaxLength(6);
            e.Property(c => c.DescActividad).HasMaxLength(150);
            e.Property(c => c.Departamento).HasMaxLength(2);
            e.Property(c => c.Municipio).HasMaxLength(2);
            e.Property(c => c.Complemento).HasMaxLength(200);
            e.Property(c => c.Telefono).HasMaxLength(30);
            e.Property(c => c.Correo).HasMaxLength(100);
            e.Property(c => c.Version).IsConcurrencyToken();
            e.HasIndex(c => c.Nombre);
            e.HasIndex(c => new { c.TipoDocumento, c.NumeroDocumento }).IsUnique()
                .HasFilter("[NumeroDocumento] IS NOT NULL");
        });

        b.Entity<Producto>(e =>
        {
            e.Property(p => p.Codigo).HasMaxLength(25).IsRequired();
            e.Property(p => p.Nombre).HasMaxLength(200).IsRequired();
            e.Property(p => p.Descripcion).HasMaxLength(800);
            e.Property(p => p.PrecioSinIva).HasPrecision(18, 2);
            e.Property(p => p.Existencias).HasPrecision(18, 4);
            e.Property(p => p.Version).IsConcurrencyToken();
            e.HasIndex(p => p.Codigo).IsUnique();
            e.HasIndex(p => p.Nombre);
        });

        b.Entity<DocumentoTributario>(e =>
        {
            e.ToTable("Documentos");
            e.Property(d => d.NumeroControl).HasMaxLength(31).IsRequired();
            e.Property(d => d.Ambiente).HasMaxLength(2).IsRequired();
            e.Property(d => d.ReceptorNombre).HasMaxLength(250).IsRequired();
            e.Property(d => d.ReceptorNombreComercial).HasMaxLength(150);
            e.Property(d => d.ReceptorTipoDocumento).HasMaxLength(2);
            e.Property(d => d.ReceptorNumDocumento).HasMaxLength(20);
            e.Property(d => d.ReceptorNrc).HasMaxLength(8);
            e.Property(d => d.ReceptorCodActividad).HasMaxLength(6);
            e.Property(d => d.ReceptorDescActividad).HasMaxLength(150);
            e.Property(d => d.ReceptorDepartamento).HasMaxLength(2);
            e.Property(d => d.ReceptorMunicipio).HasMaxLength(2);
            e.Property(d => d.ReceptorComplemento).HasMaxLength(200);
            e.Property(d => d.ReceptorTelefono).HasMaxLength(30);
            e.Property(d => d.ReceptorCorreo).HasMaxLength(100);
            e.Property(d => d.EmisorJson).IsRequired();
            e.Property(d => d.FormaPago).HasMaxLength(2);
            e.Property(d => d.TotalLetras).HasMaxLength(200).IsRequired();
            e.Property(d => d.Observaciones).HasMaxLength(500);
            e.Property(d => d.UsuarioEmisor).HasMaxLength(256).IsRequired();
            foreach (var p in new[] { nameof(DocumentoTributario.TotalGravada), nameof(DocumentoTributario.TotalDescuento), nameof(DocumentoTributario.SubTotal),
                         nameof(DocumentoTributario.TotalIva), nameof(DocumentoTributario.MontoTotalOperacion), nameof(DocumentoTributario.TotalPagar) })
                e.Property(p).HasPrecision(18, 2);
            e.Property(d => d.Version).IsConcurrencyToken();

            e.HasIndex(d => d.CodigoGeneracion).IsUnique();
            e.HasIndex(d => d.NumeroControl).IsUnique();
            e.HasIndex(d => d.ClaveIdempotencia).IsUnique();
            e.HasIndex(d => new { d.Tipo, d.FechaEmision });
            e.HasIndex(d => d.EmitidoUtc);

            e.HasOne(d => d.Cliente).WithMany(c => c.Documentos).HasForeignKey(d => d.ClienteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.DocumentoRelacionado).WithMany().HasForeignKey(d => d.DocumentoRelacionadoId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(d => d.Detalles).WithOne(x => x.Documento!).HasForeignKey(x => x.DocumentoId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(d => d.Archivos).WithOne(x => x.Documento!).HasForeignKey(x => x.DocumentoId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(d => d.Envios).WithOne(x => x.Documento!).HasForeignKey(x => x.DocumentoId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(d => d.Anulaciones).WithOne(x => x.Documento!).HasForeignKey(x => x.DocumentoId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<DetalleDocumento>(e =>
        {
            e.Property(x => x.Codigo).HasMaxLength(25).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Cantidad).HasPrecision(18, 4);
            e.Property(x => x.PrecioUnitario).HasPrecision(18, 4);
            e.Property(x => x.MontoDescuento).HasPrecision(18, 2);
            e.Property(x => x.VentaGravada).HasPrecision(18, 2);
            e.Property(x => x.IvaItem).HasPrecision(18, 2);
            e.HasIndex(x => new { x.DocumentoId, x.NumItem }).IsUnique();
            e.HasIndex(x => x.DetalleRelacionadoId);
            e.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ArchivoGenerado>(e =>
        {
            e.Property(x => x.NombreArchivo).HasMaxLength(120).IsRequired();
            e.Property(x => x.RutaRelativa).HasMaxLength(400).IsRequired();
            e.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
            e.Property(x => x.Version).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.RutaRelativa).IsUnique();
            e.HasIndex(x => x.AnulacionId);
        });

        b.Entity<EnvioCorreo>(e =>
        {
            e.ToTable("EnviosCorreo");
            e.Property(x => x.Destinatario).HasMaxLength(100).IsRequired();
            e.Property(x => x.MensajeError).HasMaxLength(500);
            e.Property(x => x.IdProveedor).HasMaxLength(200);
            e.Property(x => x.Usuario).HasMaxLength(256).IsRequired();
            e.HasIndex(x => new { x.DocumentoId, x.NumeroIntento }).IsUnique();
            e.HasIndex(x => x.Estado);
        });

        b.Entity<Anulacion>(e =>
        {
            e.ToTable("Anulaciones");
            e.Property(x => x.Motivo).HasMaxLength(250).IsRequired();
            e.Property(x => x.NombreResponsable).HasMaxLength(100).IsRequired();
            e.Property(x => x.TipoDocResponsable).HasMaxLength(2).IsRequired();
            e.Property(x => x.NumDocResponsable).HasMaxLength(20).IsRequired();
            e.Property(x => x.NombreSolicita).HasMaxLength(100).IsRequired();
            e.Property(x => x.TipoDocSolicita).HasMaxLength(2).IsRequired();
            e.Property(x => x.NumDocSolicita).HasMaxLength(20).IsRequired();
            e.Property(x => x.MensajeResultado).HasMaxLength(500);
            e.Property(x => x.Usuario).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.CodigoGeneracion).IsUnique();
            // Impide dos anulaciones vigentes (no rechazadas) del mismo documento.
            e.HasIndex(x => x.DocumentoId).IsUnique().HasFilter($"[Estado] <> {(int)EstadoAnulacion.Rechazada}")
                .HasDatabaseName("IX_Anulaciones_DocumentoId_Vigente");
        });

        b.Entity<RegistroAuditoria>(e =>
        {
            e.ToTable("Auditoria");
            e.Property(x => x.Usuario).HasMaxLength(256).IsRequired();
            e.Property(x => x.Accion).HasMaxLength(100).IsRequired();
            e.Property(x => x.Entidad).HasMaxLength(60);
            e.Property(x => x.EntidadId).HasMaxLength(60);
            e.Property(x => x.Detalle).HasMaxLength(1000);
            e.HasIndex(x => x.FechaUtc);
            e.HasIndex(x => x.Accion);
            e.HasIndex(x => x.EntidadId);
        });

        b.Entity<Respaldo>(e =>
        {
            e.Property(x => x.Usuario).HasMaxLength(256).IsRequired();
            e.Property(x => x.NombreArchivo).HasMaxLength(120).IsRequired();
            e.Property(x => x.RutaArchivo).HasMaxLength(400);
            e.Property(x => x.Mensaje).HasMaxLength(1000);
            e.HasIndex(x => x.IniciadoUtc);
        });

        b.Entity<Correlativo>(e => e.HasIndex(x => new { x.Tipo, x.Anio }).IsUnique());

        b.Entity<ConfiguracionEmisor>(e =>
        {
            e.ToTable("ConfiguracionEmisor");
            e.Property(x => x.Nit).HasMaxLength(14).IsRequired();
            e.Property(x => x.Nrc).HasMaxLength(8).IsRequired();
            e.Property(x => x.Nombre).HasMaxLength(250).IsRequired();
            e.Property(x => x.CodActividad).HasMaxLength(6).IsRequired();
            e.Property(x => x.DescActividad).HasMaxLength(150).IsRequired();
            e.Property(x => x.NombreComercial).HasMaxLength(150);
            e.Property(x => x.TipoEstablecimiento).HasMaxLength(2).IsRequired();
            e.Property(x => x.Departamento).HasMaxLength(2).IsRequired();
            e.Property(x => x.Municipio).HasMaxLength(2).IsRequired();
            e.Property(x => x.Complemento).HasMaxLength(200).IsRequired();
            e.Property(x => x.Telefono).HasMaxLength(30).IsRequired();
            e.Property(x => x.Correo).HasMaxLength(100).IsRequired();
            e.Property(x => x.CodEstable).HasMaxLength(4).IsRequired();
            e.Property(x => x.CodPuntoVenta).HasMaxLength(4).IsRequired();
            e.Property(x => x.ModificadoPor).HasMaxLength(256).IsRequired();
        });
    }

    /// <summary>La auditoría es de solo inserción: se rechaza cualquier modificación o eliminación desde la aplicación.</summary>
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ProtegerAuditoria();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProtegerAuditoria();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void ProtegerAuditoria()
    {
        if (ChangeTracker.Entries<RegistroAuditoria>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Los registros de auditoría no pueden modificarse ni eliminarse.");
    }
}

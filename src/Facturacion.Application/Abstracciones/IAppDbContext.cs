using Facturacion.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Facturacion.Application.Abstracciones;

/// <summary>Contrato del contexto de datos que usan los casos de uso (implementado con EF Core en Infrastructure).</summary>
public interface IAppDbContext
{
    DbSet<Cliente> Clientes { get; }
    DbSet<Producto> Productos { get; }
    DbSet<DocumentoTributario> Documentos { get; }
    DbSet<DetalleDocumento> DetallesDocumento { get; }
    DbSet<ArchivoGenerado> ArchivosGenerados { get; }
    DbSet<EnvioCorreo> EnviosCorreo { get; }
    DbSet<Anulacion> Anulaciones { get; }
    DbSet<RegistroAuditoria> Auditoria { get; }
    DbSet<Respaldo> Respaldos { get; }
    DbSet<Correlativo> Correlativos { get; }
    DbSet<ConfiguracionEmisor> ConfiguracionEmisor { get; }

    DatabaseFacade Database { get; }
    ChangeTracker ChangeTracker { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

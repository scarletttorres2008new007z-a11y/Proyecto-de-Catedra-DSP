using Facturacion.Application.Abstracciones;
using Facturacion.Domain;
using Facturacion.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Application.Servicios;

public sealed record ResumenPanel(
    DateOnly Desde,
    DateOnly Hasta,
    int Clientes,
    int ProductosActivos,
    int DocumentosEmitidos,
    int DocumentosEnPeriodo,
    decimal VentasPeriodo,
    decimal NotasCreditoPeriodo,
    int PendientesRevision,
    int EnviosFallidos,
    bool EmisorConfigurado,
    IReadOnlyList<DocumentoTributario> UltimosDocumentos,
    IReadOnlyList<RegistroAuditoria> UltimasOperaciones);

/// <summary>Indicadores del panel principal calculados con datos reales de la base de datos.</summary>
public sealed class PanelService(IAppDbContext db)
{
    public async Task<ResumenPanel> ObtenerAsync(DateOnly desde, DateOnly hasta, CancellationToken ct = default)
    {
        var documentosPeriodo = db.Documentos.AsNoTracking()
            .Where(d => d.FechaEmision >= desde && d.FechaEmision <= hasta && d.Estado == EstadoDocumento.Emitido);

        // Las sumas de decimal se hacen en memoria porque SQLite (pruebas) no suma decimal en el servidor.
        var importes = await documentosPeriodo.Select(d => new { d.Tipo, d.MontoTotalOperacion }).ToListAsync(ct);
        var ventas = importes.Where(i => i.Tipo != TipoDte.NotaCredito).Sum(i => i.MontoTotalOperacion);
        var notas = importes.Where(i => i.Tipo == TipoDte.NotaCredito).Sum(i => i.MontoTotalOperacion);

        // Documentos cuyo último intento de envío falló.
        var enviosFallidos = await db.Documentos.AsNoTracking()
            .CountAsync(d => d.Envios.OrderByDescending(e => e.Id).Select(e => e.Estado).FirstOrDefault() == EstadoEnvio.Fallido, ct);
        // Pendientes de revisión: envío fallido o anulación solicitada sin resolver.
        var anulacionesAbiertas = await db.Anulaciones.CountAsync(a => a.Estado == EstadoAnulacion.Solicitada, ct);

        return new ResumenPanel(
            desde, hasta,
            await db.Clientes.CountAsync(c => c.Activo, ct),
            await db.Productos.CountAsync(p => p.Activo, ct),
            await db.Documentos.CountAsync(d => d.Estado == EstadoDocumento.Emitido, ct),
            importes.Count,
            ventas,
            notas,
            enviosFallidos + anulacionesAbiertas,
            enviosFallidos,
            await db.ConfiguracionEmisor.AnyAsync(ct),
            await db.Documentos.AsNoTracking().OrderByDescending(d => d.EmitidoUtc).Take(8).ToListAsync(ct),
            await db.Auditoria.AsNoTracking().OrderByDescending(a => a.Id).Take(8).ToListAsync(ct));
    }
}

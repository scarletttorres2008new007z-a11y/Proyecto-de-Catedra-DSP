using Facturacion.Application.Abstracciones;
using Facturacion.Domain;
using Facturacion.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Application.Servicios;

public sealed record FiltroDocumentos(
    TipoDte? Tipo,
    EstadoDocumento? Estado,
    DateOnly? Desde,
    DateOnly? Hasta,
    string? Busqueda,
    bool SoloConEnvioFallido = false,
    int Pagina = 1);

public sealed record ArchivoDescarga(string Nombre, string TipoContenido, byte[] Contenido);

/// <summary>Consultas de documentos y descarga controlada de sus archivos (solo usuarios autenticados).</summary>
public sealed class DocumentoService(IAppDbContext db, IAlmacenArchivos almacen)
{
    public async Task<Pagina<DocumentoTributario>> ListarAsync(FiltroDocumentos f, CancellationToken ct = default)
    {
        var q = db.Documentos.AsNoTracking();
        if (f.Tipo is { } t) q = q.Where(d => d.Tipo == t);
        if (f.Estado is { } e) q = q.Where(d => d.Estado == e);
        if (f.Desde is { } desde) q = q.Where(d => d.FechaEmision >= desde);
        if (f.Hasta is { } hasta) q = q.Where(d => d.FechaEmision <= hasta);
        if (f.SoloConEnvioFallido)
            q = q.Where(d => d.Envios.OrderByDescending(x => x.Id).Select(x => x.Estado).FirstOrDefault() == EstadoEnvio.Fallido);
        if (!string.IsNullOrWhiteSpace(f.Busqueda))
        {
            var b = f.Busqueda.Trim();
            var codigo = Guid.TryParse(b, out var g) ? g : (Guid?)null;
            q = q.Where(d => d.NumeroControl.Contains(b)
                             || d.ReceptorNombre.Contains(b)
                             || (d.ReceptorNumDocumento != null && d.ReceptorNumDocumento.Contains(b))
                             || (codigo != null && d.CodigoGeneracion == codigo));
        }

        var pagina = PaginacionExtensions.Normalizar(f.Pagina);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(d => d.EmitidoUtc).ThenByDescending(d => d.Id)
            .Skip((pagina - 1) * PaginacionExtensions.TamanoPorDefecto).Take(PaginacionExtensions.TamanoPorDefecto)
            .Include(d => d.Envios)
            .ToListAsync(ct);
        return new Pagina<DocumentoTributario>(items, pagina, PaginacionExtensions.TamanoPorDefecto, total);
    }

    public Task<DocumentoTributario?> ObtenerDetalleAsync(int id, CancellationToken ct = default) =>
        db.Documentos.AsNoTracking()
            .Include(d => d.Detalles)
            .Include(d => d.Archivos)
            .Include(d => d.Envios)
            .Include(d => d.Anulaciones)
            .Include(d => d.DocumentoRelacionado)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<List<DocumentoTributario>> NotasCreditoDeAsync(int documentoId, CancellationToken ct = default) =>
        db.Documentos.AsNoTracking().Where(d => d.DocumentoRelacionadoId == documentoId).OrderBy(d => d.EmitidoUtc).ToListAsync(ct);

    /// <summary>CCF vigentes que pueden recibir notas de crédito.</summary>
    public Task<List<DocumentoTributario>> CreditosFiscalesVigentesAsync(CancellationToken ct = default) =>
        db.Documentos.AsNoTracking()
            .Where(d => d.Tipo == TipoDte.ComprobanteCreditoFiscal && d.Estado == EstadoDocumento.Emitido
                        && !db.Anulaciones.Any(a => a.DocumentoId == d.Id && a.Estado != EstadoAnulacion.Rechazada))
            .OrderByDescending(d => d.EmitidoUtc).Take(200).ToListAsync(ct);

    public async Task<ArchivoDescarga?> ObtenerArchivoAsync(int documentoId, int archivoId, CancellationToken ct = default)
    {
        var archivo = await db.ArchivosGenerados.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == archivoId && a.DocumentoId == documentoId, ct);
        if (archivo is null) return null;
        var contenido = await almacen.LeerAsync(archivo.RutaRelativa, ct);
        if (contenido is null) return null;
        var tipo = archivo.Proposito == PropositoArchivo.DocumentoPdf ? "application/pdf" : "application/json";
        return new ArchivoDescarga(archivo.NombreArchivo, tipo, contenido);
    }

    public Task<List<EnvioCorreo>> EnviosAsync(int documentoId, CancellationToken ct = default) =>
        db.EnviosCorreo.AsNoTracking().Where(e => e.DocumentoId == documentoId).OrderBy(e => e.NumeroIntento).ToListAsync(ct);
}

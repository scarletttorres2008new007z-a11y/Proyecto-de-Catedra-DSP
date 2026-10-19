using System.Text;
using Facturacion.Application.Abstracciones;
using Facturacion.Application.Configuracion;
using Facturacion.Application.Dte;
using Facturacion.Domain;
using Facturacion.Domain.Entidades;
using Facturacion.Domain.Reglas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Facturacion.Application.Servicios;

public sealed class SolicitudAnulacion
{
    public int DocumentoId { get; set; }
    public TipoAnulacion Tipo { get; set; }
    public string? Motivo { get; set; }
    public int? DocumentoReemplazoId { get; set; }
    public string? NombreResponsable { get; set; }
    public string? TipoDocResponsable { get; set; }
    public string? NumDocResponsable { get; set; }
    public string? NombreSolicita { get; set; }
    public string? TipoDocSolicita { get; set; }
    public string? NumDocSolicita { get; set; }
}

public sealed record ContextoAnulacion(
    DocumentoTributario Documento,
    PlazoAnulacion Plazo,
    DateTime FechaLimiteLocal,
    bool Permitida,
    IReadOnlyList<string> Impedimentos,
    IReadOnlyList<DocumentoTributario> CandidatosReemplazo);

public sealed record ResultadoAnulacion(int AnulacionId, EstadoAnulacion Estado, string Mensaje, int? EnvioId);

/// <summary>
/// Flujo de anulación (invalidación): valida estado y plazo configurado, registra la solicitud, la procesa
/// con el transmisor disponible (simulado en modo académico), actualiza el estado del documento solo si el
/// resultado lo justifica, devuelve existencias y, únicamente después de procesada, notifica al cliente.
/// </summary>
public sealed class AnulacionService(
    IAppDbContext db,
    IReloj reloj,
    IUsuarioActual usuario,
    IAuditoria auditoria,
    IAlmacenArchivos almacen,
    ITransmisorHacienda transmisor,
    CorreoService correo,
    IOptions<OpcionesAnulacion> opciones,
    ILogger<AnulacionService> logger)
{
    public async Task<ContextoAnulacion?> ObtenerContextoAsync(int documentoId, CancellationToken ct = default)
    {
        var doc = await db.Documentos.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentoId, ct);
        if (doc is null) return null;

        var plazo = opciones.Value.Plazo(doc.Tipo);
        var limite = ReglasAnulacion.FechaLimiteLocal(doc.FechaEmision, plazo);
        var validacion = await ValidarAsync(doc, TipoAnulacion.RescindirOperacion, "motivo de prueba", null, ct);
        var candidatos = await db.Documentos.AsNoTracking()
            .Where(d => d.Tipo == doc.Tipo && d.Id != doc.Id && d.Estado == EstadoDocumento.Emitido && d.EmitidoUtc >= doc.EmitidoUtc)
            .OrderByDescending(d => d.EmitidoUtc).Take(50).ToListAsync(ct);

        return new ContextoAnulacion(doc, plazo, limite, validacion.EsValido, validacion.Errores.Select(e => e.Mensaje).ToList(), candidatos);
    }

    private async Task<ResultadoValidacion> ValidarAsync(DocumentoTributario doc, TipoAnulacion tipo, string? motivo, int? reemplazoId, CancellationToken ct)
    {
        var vigente = await db.Anulaciones.AnyAsync(a => a.DocumentoId == doc.Id && a.Estado != EstadoAnulacion.Rechazada, ct);
        var notasVigentes = await db.Documentos.AnyAsync(d => d.DocumentoRelacionadoId == doc.Id && d.Estado == EstadoDocumento.Emitido, ct);
        var reemplazo = reemplazoId is { } rid ? await db.Documentos.AsNoTracking().FirstOrDefaultAsync(d => d.Id == rid, ct) : null;
        var r = ReglasAnulacion.Validar(doc, reloj.AhoraLocal, opciones.Value.Plazo(doc.Tipo), tipo, motivo, reemplazo, vigente, notasVigentes);
        if (reemplazoId is not null && reemplazo is null) r.Agregar("DocumentoReemplazoId", "El documento de reemplazo no existe.");
        return r;
    }

    private static ResultadoValidacion ValidarSolicitante(SolicitudAnulacion s)
    {
        var r = new ResultadoValidacion();
        void Persona(string? nombre, string? tipo, string? numero, string campo, string rol)
        {
            if (string.IsNullOrWhiteSpace(nombre) || nombre.Trim().Length > 100) r.Agregar($"Nombre{campo}", $"Ingrese el nombre de quien {rol} (máximo 100 caracteres).");
            if (string.IsNullOrWhiteSpace(tipo)) { r.Agregar($"TipoDoc{campo}", $"Seleccione el tipo de documento de quien {rol}."); return; }
            var error = IdentificadoresFiscales.ValidarDocumento(tipo, IdentificadoresFiscales.NormalizarDocumento(tipo, numero));
            if (error is not null) r.Agregar($"NumDoc{campo}", $"Documento de quien {rol}: {error}");
        }
        Persona(s.NombreResponsable, s.TipoDocResponsable, s.NumDocResponsable, "Responsable", "es responsable de la anulación");
        Persona(s.NombreSolicita, s.TipoDocSolicita, s.NumDocSolicita, "Solicita", "solicita la anulación");
        return r;
    }

    public async Task<ResultadoAnulacion> SolicitarAsync(SolicitudAnulacion s, CancellationToken ct = default)
    {
        var doc = await db.Documentos.Include(d => d.Detalles).FirstOrDefaultAsync(d => d.Id == s.DocumentoId, ct)
                  ?? throw new ReglaNegocioException("El documento no existe.");

        var r = await ValidarAsync(doc, s.Tipo, s.Motivo, s.DocumentoReemplazoId, ct);
        r.Agregar(ValidarSolicitante(s));
        if (!r.EsValido)
        {
            await auditoria.RegistrarAsync("Anulación rechazada por validación", nameof(DocumentoTributario), doc.Id.ToString(),
                ResultadoOperacion.Error, r.ToString(), ct);
            throw new ReglaNegocioException(r);
        }

        var reemplazo = s.DocumentoReemplazoId is { } rid ? await db.Documentos.AsNoTracking().FirstAsync(d => d.Id == rid, ct) : null;
        var emisor = EmisorSnapshot.Deserializar(doc.EmisorJson);
        var ahoraUtc = reloj.UtcNow;

        var anulacion = new Anulacion
        {
            DocumentoId = doc.Id,
            CodigoGeneracion = Guid.NewGuid(),
            Tipo = s.Tipo,
            Motivo = s.Motivo!.Trim(),
            DocumentoReemplazoId = reemplazo?.Id,
            CodigoGeneracionReemplazo = reemplazo?.CodigoGeneracion,
            NombreResponsable = s.NombreResponsable!.Trim(),
            TipoDocResponsable = s.TipoDocResponsable!,
            NumDocResponsable = IdentificadoresFiscales.NormalizarDocumento(s.TipoDocResponsable, s.NumDocResponsable)!,
            NombreSolicita = s.NombreSolicita!.Trim(),
            TipoDocSolicita = s.TipoDocSolicita!,
            NumDocSolicita = IdentificadoresFiscales.NormalizarDocumento(s.TipoDocSolicita, s.NumDocSolicita)!,
            Estado = EstadoAnulacion.Solicitada,
            Modo = transmisor.Modo,
            SolicitadaUtc = ahoraUtc,
            Usuario = usuario.Nombre
        };

        // 1) Registrar la solicitud. El índice único filtrado impide dos anulaciones vigentes del mismo documento.
        db.Anulaciones.Add(anulacion);
        auditoria.Agregar("Anulación solicitada", nameof(DocumentoTributario), doc.Id.ToString(), ResultadoOperacion.Exito,
            $"Tipo {(int)s.Tipo}, motivo: {anulacion.Motivo}");
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            throw new ReglaNegocioException("El documento ya tiene una anulación registrada.");
        }

        // 2) Generar el evento JSON y procesarlo con el transmisor disponible.
        string? rutaEvento = null;
        try
        {
            var json = GeneradorAnulacionJson.Generar(anulacion, doc, emisor, reloj.ALocal(ahoraUtc));
            var nombre = $"{GeneradorDteJson.Codigo(anulacion.CodigoGeneracion)}.json";
            var escrito = await almacen.GuardarNuevoAsync($"anulaciones/{reloj.ALocal(ahoraUtc):yyyy}/{nombre}", new UTF8Encoding(false).GetBytes(json), ct);
            rutaEvento = escrito.RutaRelativa;

            var resultado = await transmisor.InvalidarAsync(anulacion, json, ct);

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.ArchivosGenerados.Add(new ArchivoGenerado
            {
                DocumentoId = doc.Id,
                AnulacionId = anulacion.Id,
                Proposito = PropositoArchivo.AnulacionJson,
                NombreArchivo = nombre,
                RutaRelativa = escrito.RutaRelativa,
                TamanoBytes = escrito.TamanoBytes,
                Sha256 = escrito.Sha256,
                Version = $"anulacion-v{GeneradorAnulacionJson.Version}",
                GeneradoUtc = ahoraUtc
            });

            anulacion.ResueltaUtc = reloj.UtcNow;
            anulacion.MensajeResultado = resultado.Mensaje;
            if (resultado.Procesado)
            {
                anulacion.Estado = resultado.Simulado ? EstadoAnulacion.ProcesadaSimulada : EstadoAnulacion.ProcesadaOficial;
                doc.Estado = EstadoDocumento.Anulado;
                doc.Version = Guid.NewGuid();
                await DevolverInventarioAsync(doc, ct);
            }
            else
            {
                anulacion.Estado = EstadoAnulacion.Rechazada;
            }
            auditoria.Agregar(resultado.Procesado
                    ? (resultado.Simulado ? "Anulación procesada (simulada)" : "Anulación procesada")
                    : "Anulación rechazada",
                nameof(DocumentoTributario), doc.Id.ToString(),
                resultado.Procesado ? ResultadoOperacion.Exito : ResultadoOperacion.Error, resultado.Mensaje);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex) when (ex is not ReglaNegocioException)
        {
            logger.LogError(ex, "Error al procesar la anulación {AnulacionId}", anulacion.Id);
            db.ChangeTracker.Clear();
            if (rutaEvento is not null)
            {
                try { almacen.Eliminar(rutaEvento); } catch (Exception e) { logger.LogWarning(e, "No se pudo eliminar {Ruta}", rutaEvento); }
            }
            // La solicitud queda registrada como rechazada para permitir un nuevo intento.
            await db.Anulaciones.Where(a => a.Id == anulacion.Id).ExecuteUpdateAsync(u => u
                .SetProperty(a => a.Estado, EstadoAnulacion.Rechazada)
                .SetProperty(a => a.ResueltaUtc, reloj.UtcNow)
                .SetProperty(a => a.MensajeResultado, "Error interno al procesar la anulación. El documento no cambió de estado."), CancellationToken.None);
            await auditoria.RegistrarAsync("Anulación fallida", nameof(DocumentoTributario), doc.Id.ToString(), ResultadoOperacion.Error,
                $"Error interno ({ex.GetType().Name}). El documento conserva su estado.", CancellationToken.None);
            throw new ReglaNegocioException("No se pudo procesar la anulación. El documento conserva su estado; puede intentarlo de nuevo.");
        }

        // 3) Notificar al cliente solo cuando la anulación quedó procesada.
        int? envioId = null;
        if (anulacion.Estado is EstadoAnulacion.ProcesadaSimulada or EstadoAnulacion.ProcesadaOficial)
        {
            try
            {
                envioId = (await correo.SolicitarEnvioAsync(doc.Id, MotivoEnvio.NotificacionAnulacion, null, anulacion.Id, ct)).Id;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "No se pudo registrar la notificación de la anulación {AnulacionId}", anulacion.Id);
            }
        }

        return new ResultadoAnulacion(anulacion.Id, anulacion.Estado, anulacion.MensajeResultado ?? "", envioId);
    }

    /// <summary>La anulación de facturas y CCF devuelve las existencias descontadas al emitir.</summary>
    private async Task DevolverInventarioAsync(DocumentoTributario doc, CancellationToken ct)
    {
        if (doc.Tipo == TipoDte.NotaCredito) return;
        foreach (var g in doc.Detalles.Where(d => d.ProductoId != null).GroupBy(d => d.ProductoId!.Value))
        {
            var cantidad = g.Sum(x => x.Cantidad);
            var version = Guid.NewGuid();
            await db.Productos.Where(p => p.Id == g.Key && p.ControlaInventario)
                .ExecuteUpdateAsync(u => u.SetProperty(p => p.Existencias, p => p.Existencias + cantidad)
                                          .SetProperty(p => p.Version, version), ct);
        }
    }

    public async Task<EnvioCorreo> ReenviarNotificacionAsync(int anulacionId, string? destinatarioAlternativo, CancellationToken ct = default)
    {
        var docId = await db.Anulaciones.AsNoTracking().Where(a => a.Id == anulacionId).Select(a => (int?)a.DocumentoId).FirstOrDefaultAsync(ct)
                    ?? throw new ReglaNegocioException("La anulación no existe.");
        return await correo.SolicitarEnvioAsync(docId, MotivoEnvio.NotificacionAnulacion, destinatarioAlternativo, anulacionId, ct);
    }

    public async Task<Pagina<Anulacion>> ListarAsync(EstadoAnulacion? estado, int pagina, CancellationToken ct = default)
    {
        var q = db.Anulaciones.AsNoTracking().Include(a => a.Documento).AsQueryable();
        if (estado is { } e) q = q.Where(a => a.Estado == e);
        pagina = PaginacionExtensions.Normalizar(pagina);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.SolicitadaUtc)
            .Skip((pagina - 1) * PaginacionExtensions.TamanoPorDefecto).Take(PaginacionExtensions.TamanoPorDefecto).ToListAsync(ct);
        return new Pagina<Anulacion>(items, pagina, PaginacionExtensions.TamanoPorDefecto, total);
    }
}

using System.Net;
using System.Security.Cryptography;
using System.Text;
using Facturacion.Application.Abstracciones;
using Facturacion.Application.Dte;
using Facturacion.Domain;
using Facturacion.Domain.Entidades;
using Facturacion.Domain.Reglas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Facturacion.Application.Servicios;

/// <summary>
/// Envío de documentos por correo. Cada intento es un registro en EnviosCorreo (pendiente → enviado al
/// servidor SMTP / fallido). Los reenvíos reutilizan los archivos ya generados y nunca emiten un documento nuevo.
/// </summary>
public sealed class CorreoService(
    IAppDbContext db,
    IReloj reloj,
    IUsuarioActual usuario,
    IAuditoria auditoria,
    IAlmacenArchivos almacen,
    IEnviadorCorreo enviador,
    IColaCorreo cola,
    ILogger<CorreoService> logger)
{
    /// <summary>Registra un nuevo intento (reenvío o notificación) y lo encola.</summary>
    public async Task<EnvioCorreo> SolicitarEnvioAsync(int documentoId, MotivoEnvio motivo, string? destinatarioAlternativo = null,
        int? anulacionId = null, CancellationToken ct = default)
    {
        var doc = await db.Documentos.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentoId, ct)
                  ?? throw new ReglaNegocioException("El documento no existe.");

        string destinatario;
        if (!string.IsNullOrWhiteSpace(destinatarioAlternativo))
        {
            destinatario = destinatarioAlternativo.Trim().ToLowerInvariant();
            if (!IdentificadoresFiscales.EsCorreoValido(destinatario))
                throw new ReglaNegocioException("El correo alternativo no es válido.", "Destinatario");
        }
        else destinatario = doc.ReceptorCorreo ?? "";

        if (motivo == MotivoEnvio.NotificacionAnulacion)
        {
            var anulacion = await db.Anulaciones.AsNoTracking().FirstOrDefaultAsync(a => a.Id == anulacionId && a.DocumentoId == documentoId, ct);
            if (anulacion is null || anulacion.Estado is not (EstadoAnulacion.ProcesadaSimulada or EstadoAnulacion.ProcesadaOficial))
                throw new ReglaNegocioException("Solo se notifica una anulación que ya fue procesada.");
        }

        var intento = await db.EnviosCorreo.Where(e => e.DocumentoId == documentoId).MaxAsync(e => (int?)e.NumeroIntento, ct) ?? 0;
        var envio = new EnvioCorreo
        {
            DocumentoId = documentoId,
            AnulacionId = anulacionId,
            TipoDocumento = doc.Tipo,
            Motivo = motivo,
            Destinatario = destinatario,
            NumeroIntento = intento + 1,
            Estado = EstadoEnvio.Pendiente,
            CreadoUtc = reloj.UtcNow,
            Usuario = usuario.Nombre
        };
        db.EnviosCorreo.Add(envio);
        auditoria.Agregar(motivo switch
        {
            MotivoEnvio.Reenvio => "Reenvío de documento solicitado",
            MotivoEnvio.NotificacionAnulacion => "Notificación de anulación solicitada",
            _ => "Envío de documento solicitado"
        }, nameof(DocumentoTributario), documentoId.ToString(), ResultadoOperacion.Exito, $"Intento {envio.NumeroIntento}");
        await db.SaveChangesAsync(ct);
        cola.Encolar(envio.Id);
        return envio;
    }

    /// <summary>Procesa un envío pendiente: valida, adjunta, envía y registra el resultado.</summary>
    public async Task<EstadoEnvio> ProcesarAsync(int envioId, CancellationToken ct = default)
    {
        var envio = await db.EnviosCorreo.FirstOrDefaultAsync(e => e.Id == envioId, ct);
        if (envio is null || envio.Estado != EstadoEnvio.Pendiente) return envio?.Estado ?? EstadoEnvio.Fallido;

        var doc = await db.Documentos.AsNoTracking().Include(d => d.Archivos).FirstAsync(d => d.Id == envio.DocumentoId, ct);
        string? idProveedor = null;
        string? error = null;

        try
        {
            if (string.IsNullOrWhiteSpace(envio.Destinatario))
                throw new CorreoException(TipoFalloCorreo.DestinatarioInvalido, "El cliente no tiene un correo electrónico registrado.");
            if (!IdentificadoresFiscales.EsCorreoValido(envio.Destinatario))
                throw new CorreoException(TipoFalloCorreo.DestinatarioInvalido, "El correo del destinatario no es válido.");

            var mensaje = envio.Motivo == MotivoEnvio.NotificacionAnulacion
                ? await ConstruirNotificacionAnulacionAsync(envio, doc, ct)
                : await ConstruirMensajeDocumentoAsync(envio, doc, ct);
            idProveedor = await enviador.EnviarAsync(mensaje, ct);
        }
        catch (CorreoException ex)
        {
            error = ex.Message;
            logger.LogWarning(ex.InnerException, "Envío {EnvioId} fallido ({Tipo})", envioId, ex.Tipo);
        }
        catch (ArchivoNoDisponibleException ex)
        {
            error = ex.Message;
            logger.LogWarning("Envío {EnvioId} fallido: {Mensaje}", envioId, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            error = "Error inesperado al preparar o enviar el correo. Consulte el registro del servidor.";
            logger.LogError(ex, "Error inesperado en el envío {EnvioId}", envioId);
        }

        envio.Estado = error is null ? EstadoEnvio.EnviadoServidorSmtp : EstadoEnvio.Fallido;
        envio.MensajeError = error;
        envio.IdProveedor = idProveedor?.Length > 200 ? idProveedor[..200] : idProveedor;
        envio.ProcesadoUtc = reloj.UtcNow;
        auditoria.Agregar(error is null ? "Correo aceptado por el servidor SMTP" : "Envío de correo fallido",
            nameof(DocumentoTributario), doc.Id.ToString(),
            error is null ? ResultadoOperacion.Exito : ResultadoOperacion.Error,
            $"{envio.Motivo}, intento {envio.NumeroIntento}" + (error is null ? "" : $": {error}"));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // El correo pudo haberse enviado, pero el resultado no quedó guardado: queda en el log del servidor.
            logger.LogError(ex, "No se pudo registrar el resultado del envío {EnvioId} (estado {Estado})", envioId, envio.Estado);
            throw;
        }
        return envio.Estado;
    }

    private async Task<MensajeCorreo> ConstruirMensajeDocumentoAsync(EnvioCorreo envio, DocumentoTributario doc, CancellationToken ct)
    {
        var json = await LeerVerificadoAsync(doc, PropositoArchivo.DocumentoJson, ct);
        var pdf = await LeerVerificadoAsync(doc, PropositoArchivo.DocumentoPdf, ct);
        var codigo = GeneradorDteJson.Codigo(doc.CodigoGeneracion);
        if (!Encoding.UTF8.GetString(json.Contenido).Contains(codigo))
            throw new ArchivoNoDisponibleException("El archivo JSON no corresponde al documento.");

        var emisor = EmisorSnapshot.Deserializar(doc.EmisorJson);
        var nombreTipo = TiposDteInfo.Nombre(doc.Tipo);
        var asunto = $"{nombreTipo} {doc.NumeroControl}" + (envio.Motivo == MotivoEnvio.Reenvio ? " (reenvío)" : "");

        var lineas = new List<string>
        {
            $"Estimado(a) {doc.ReceptorNombre}:",
            "",
            $"Le enviamos su {nombreTipo.ToLowerInvariant()} emitida por {emisor.NombreComercial ?? emisor.Nombre}.",
            "",
            $"Número de control: {doc.NumeroControl}",
            $"Código de generación: {codigo}",
            $"Fecha de emisión: {doc.FechaEmision:dd/MM/yyyy}",
            $"Total: US$ {doc.TotalPagar:0.00}",
            "",
            "Archivos adjuntos:",
            $" - {json.Nombre}: datos estructurados del documento (JSON).",
            $" - {pdf.Nombre}: representación gráfica del documento (PDF).",
        };
        AgregarAviso(doc, lineas);
        AgregarContacto(emisor, lineas);

        return new MensajeCorreo(envio.Destinatario, doc.ReceptorNombre, asunto, string.Join("\n", lineas), Html(lineas),
            new[]
            {
                new AdjuntoCorreo(json.Nombre, "application/json", json.Contenido),
                new AdjuntoCorreo(pdf.Nombre, "application/pdf", pdf.Contenido)
            });
    }

    private async Task<MensajeCorreo> ConstruirNotificacionAnulacionAsync(EnvioCorreo envio, DocumentoTributario doc, CancellationToken ct)
    {
        var anulacion = await db.Anulaciones.AsNoTracking().FirstAsync(a => a.Id == envio.AnulacionId, ct);
        var archivo = doc.Archivos.FirstOrDefault(a => a.Proposito == PropositoArchivo.AnulacionJson && a.AnulacionId == anulacion.Id)
                      ?? throw new ArchivoNoDisponibleException("No se encontró el archivo del evento de anulación.");
        var evento = await LeerVerificadoAsync(archivo, ct);
        var emisor = EmisorSnapshot.Deserializar(doc.EmisorJson);
        var simulada = anulacion.Estado == EstadoAnulacion.ProcesadaSimulada;

        var lineas = new List<string>
        {
            $"Estimado(a) {doc.ReceptorNombre}:",
            "",
            $"Le informamos que el documento {TiposDteInfo.Nombre(doc.Tipo).ToLowerInvariant()} con número de control {doc.NumeroControl} fue anulado.",
            "",
            $"Código de generación del documento: {GeneradorDteJson.Codigo(doc.CodigoGeneracion)}",
            $"Código de generación del evento de anulación: {GeneradorDteJson.Codigo(anulacion.CodigoGeneracion)}",
            $"Motivo: {anulacion.Motivo}",
            "",
            $"Se adjunta el evento de anulación ({evento.Nombre}).",
        };
        if (simulada)
        {
            lineas.Add("");
            lineas.Add("AVISO: anulación procesada en modo de demostración académica. No es una invalidación oficial ante el Ministerio de Hacienda.");
        }
        AgregarContacto(emisor, lineas);

        return new MensajeCorreo(envio.Destinatario, doc.ReceptorNombre, $"Anulación de {TiposDteInfo.NombreCorto(doc.Tipo)} {doc.NumeroControl}",
            string.Join("\n", lineas), Html(lineas), new[] { new AdjuntoCorreo(evento.Nombre, "application/json", evento.Contenido) });
    }

    private static void AgregarAviso(DocumentoTributario doc, List<string> lineas)
    {
        if (doc.Modo != ModoOperacion.Academico) return;
        lineas.Add("");
        lineas.Add("AVISO: documento generado en modo de demostración académica. No ha sido transmitido, validado ni aceptado por el Ministerio de Hacienda.");
    }

    private static void AgregarContacto(EmisorSnapshot e, List<string> lineas)
    {
        lineas.Add("");
        lineas.Add($"{e.NombreComercial ?? e.Nombre}");
        lineas.Add($"Teléfono: {e.Telefono} · Correo: {e.Correo}");
    }

    private static string Html(IEnumerable<string> lineas) =>
        "<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1f2933\">" +
        string.Join("<br>", lineas.Select(WebUtility.HtmlEncode)) + "</div>";

    private sealed record ArchivoLeido(string Nombre, byte[] Contenido);

    private Task<ArchivoLeido> LeerVerificadoAsync(DocumentoTributario doc, PropositoArchivo proposito, CancellationToken ct)
    {
        var archivo = doc.Archivos.Where(a => a.Proposito == proposito).OrderByDescending(a => a.Id).FirstOrDefault()
                      ?? throw new ArchivoNoDisponibleException($"No se encontró el archivo {(proposito == PropositoArchivo.DocumentoJson ? "JSON" : "PDF")} del documento.");
        return LeerVerificadoAsync(archivo, ct);
    }

    /// <summary>Comprueba que el archivo existe, no está vacío y su huella SHA-256 coincide con la registrada.</summary>
    private async Task<ArchivoLeido> LeerVerificadoAsync(ArchivoGenerado archivo, CancellationToken ct)
    {
        var contenido = await almacen.LeerAsync(archivo.RutaRelativa, ct)
                        ?? throw new ArchivoNoDisponibleException($"El archivo {archivo.NombreArchivo} no existe en el almacenamiento.");
        if (contenido.Length == 0) throw new ArchivoNoDisponibleException($"El archivo {archivo.NombreArchivo} está vacío.");
        var hash = Convert.ToHexString(SHA256.HashData(contenido));
        if (!hash.Equals(archivo.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new ArchivoNoDisponibleException($"El archivo {archivo.NombreArchivo} fue modificado después de generarse.");
        return new ArchivoLeido(archivo.NombreArchivo, contenido);
    }
}

public sealed class ArchivoNoDisponibleException(string mensaje) : Exception(mensaje);

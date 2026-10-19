using System.Text;
using System.Text.Json;
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

public sealed class LineaSolicitud
{
    public int ProductoId { get; set; }
    public decimal Cantidad { get; set; }
    public decimal Descuento { get; set; }
}

public sealed class LineaNotaCreditoSolicitud
{
    public int DetalleRelacionadoId { get; set; }
    public decimal Cantidad { get; set; }
}

/// <summary>Datos que envía el formulario de emisión. Los importes nunca vienen del navegador.</summary>
public sealed class SolicitudEmision
{
    public TipoDte Tipo { get; set; }
    public int ClienteId { get; set; }
    public string FormaPago { get; set; } = "01";
    public string? Observaciones { get; set; }
    public List<LineaSolicitud> Lineas { get; set; } = new();

    // Solo nota de crédito.
    public int? DocumentoRelacionadoId { get; set; }
    public List<LineaNotaCreditoSolicitud> LineasNotaCredito { get; set; } = new();
}

public sealed record LineaPreparada(
    int NumItem,
    int? ProductoId,
    int? DetalleRelacionadoId,
    string Codigo,
    string Descripcion,
    int UnidadMedida,
    LineaCalculada Calculo);

public sealed record DocumentoPreparado(
    TipoDte Tipo,
    Cliente Cliente,
    DocumentoTributario? Relacionado,
    IReadOnlyList<LineaPreparada> Lineas,
    ResumenCalculado Resumen,
    string TotalLetras,
    string FormaPago,
    string? Observaciones);

public sealed record ResultadoEmision(int DocumentoId, bool YaExistia, int? EnvioId);

/// <summary>Línea de un CCF con lo que queda por acreditar (para la pantalla de nota de crédito).</summary>
public sealed record LineaAcreditable(DetalleDocumento Linea, decimal YaAcreditado)
{
    public decimal Disponible => Linea.Cantidad - YaAcreditado;
}

/// <summary>
/// Caso de uso de emisión: prepara (vista previa calculada en el servidor) y emite el documento en una
/// transacción que incluye correlativo, inventario, documento, detalles, archivos JSON/PDF y el registro
/// del envío pendiente. Si algo falla, nada queda guardado y los archivos escritos se eliminan.
/// </summary>
public sealed class EmisionService(
    IAppDbContext db,
    IReloj reloj,
    IUsuarioActual usuario,
    IAuditoria auditoria,
    IAlmacenArchivos almacen,
    IGeneradorPdf generadorPdf,
    ITransmisorHacienda transmisor,
    IColaCorreo colaCorreo,
    IOptions<OpcionesFacturacion> opciones,
    ILogger<EmisionService> logger)
{
    public async Task<DocumentoPreparado> PrepararAsync(SolicitudEmision s, CancellationToken ct = default)
    {
        var r = new ResultadoValidacion();
        if (!Enum.IsDefined(s.Tipo)) throw new ReglaNegocioException("Seleccione un tipo de documento válido.", "Tipo");
        if (!Catalogos.FormasPago.ContainsKey(s.FormaPago ?? "")) r.Agregar(nameof(s.FormaPago), "Seleccione una forma de pago válida.");
        var observaciones = string.IsNullOrWhiteSpace(s.Observaciones) ? null : s.Observaciones.Trim();
        if (observaciones?.Length > 500) r.Agregar(nameof(s.Observaciones), "Las observaciones admiten como máximo 500 caracteres.");

        var emisor = await db.ConfiguracionEmisor.AsNoTracking().FirstOrDefaultAsync(ct);
        if (emisor is null || !ConfiguracionEmisorService.Validar(emisor).EsValido)
            r.Agregar("Emisor", "Configure los datos del emisor (menú Configuración) antes de emitir documentos.");

        DocumentoTributario? relacionado = null;
        Cliente? cliente;
        List<(LineaPreparadaBase Base, LineaEntrada Entrada)> lineas;

        if (s.Tipo == TipoDte.NotaCredito)
        {
            relacionado = s.DocumentoRelacionadoId is { } rid
                ? await db.Documentos.AsNoTracking().Include(d => d.Detalles).FirstOrDefaultAsync(d => d.Id == rid, ct)
                : null;
            var enCurso = relacionado is not null && await TieneAnulacionVigenteAsync(relacionado.Id, ct);
            r.Agregar(ReglasDocumento.ValidarDocumentoBaseNotaCredito(relacionado, enCurso));
            if (!r.EsValido) throw new ReglaNegocioException(r);

            // El receptor de la nota de crédito es el mismo del CCF relacionado.
            cliente = await db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == relacionado!.ClienteId, ct);
            lineas = await PrepararLineasNotaCreditoAsync(relacionado!, s.LineasNotaCredito, r, ct);
        }
        else
        {
            cliente = await db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == s.ClienteId, ct);
            if (cliente is null) r.Agregar(nameof(s.ClienteId), "Seleccione un cliente.");
            lineas = await PrepararLineasVentaAsync(s.Lineas, r, ct);
        }

        if (!r.EsValido) throw new ReglaNegocioException(r);

        var resumen = CalculadoraImportes.Calcular(s.Tipo, lineas.Select(l => l.Entrada).ToList());

        r.Agregar(ReglasDocumento.ValidarReceptor(s.Tipo, cliente!, resumen.MontoTotalOperacion, opciones.Value.UmbralIdentificacionFactura));
        if (relacionado is not null)
        {
            var acreditadoPrevio = await db.Documentos.AsNoTracking()
                .Where(d => d.DocumentoRelacionadoId == relacionado.Id && d.Estado == EstadoDocumento.Emitido)
                .SumAsync(d => d.MontoTotalOperacion, ct);
            if (acreditadoPrevio + resumen.MontoTotalOperacion > relacionado.MontoTotalOperacion)
                r.Agregar("Lineas", $"El total acreditado ({acreditadoPrevio + resumen.MontoTotalOperacion:0.00}) superaría el total del CCF ({relacionado.MontoTotalOperacion:0.00}).");
        }
        if (!r.EsValido) throw new ReglaNegocioException(r);

        var preparadas = lineas.Select((l, i) => new LineaPreparada(i + 1, l.Base.ProductoId, l.Base.DetalleRelacionadoId,
            l.Base.Codigo, l.Base.Descripcion, l.Base.UnidadMedida, resumen.Lineas[i])).ToList();

        return new DocumentoPreparado(s.Tipo, cliente!, relacionado, preparadas, resumen,
            NumeroALetras.Convertir(resumen.TotalPagar), s.FormaPago!, observaciones);
    }

    private sealed record LineaPreparadaBase(int? ProductoId, int? DetalleRelacionadoId, string Codigo, string Descripcion, int UnidadMedida);

    private async Task<List<(LineaPreparadaBase, LineaEntrada)>> PrepararLineasVentaAsync(
        List<LineaSolicitud> solicitadas, ResultadoValidacion r, CancellationToken ct)
    {
        var lineas = solicitadas.Where(l => l.ProductoId > 0).ToList();
        if (lineas.Count == 0)
        {
            r.Agregar("Lineas", "Agregue al menos un producto al documento.");
            return new();
        }

        var ids = lineas.Select(l => l.ProductoId).Distinct().ToList();
        var productos = await db.Productos.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var resultado = new List<(LineaPreparadaBase, LineaEntrada)>();

        foreach (var (l, i) in lineas.Select((l, i) => (l, i)))
        {
            if (!productos.TryGetValue(l.ProductoId, out var p) || !p.Activo)
            {
                r.Agregar($"Lineas[{i}].ProductoId", $"Línea {i + 1}: el producto no existe o está inactivo.");
                continue;
            }
            var descripcion = p.Descripcion is null ? p.Nombre : $"{p.Nombre} - {p.Descripcion}";
            if (descripcion.Length > 1000) descripcion = descripcion[..1000];
            resultado.Add((new LineaPreparadaBase(p.Id, null, p.Codigo, descripcion, p.UnidadMedida),
                new LineaEntrada(l.Cantidad, p.PrecioSinIva, l.Descuento)));
        }

        // Disponibilidad de inventario (se vuelve a garantizar dentro de la transacción).
        foreach (var g in lineas.GroupBy(l => l.ProductoId))
        {
            if (productos.TryGetValue(g.Key, out var p) && p.ControlaInventario && g.Sum(x => x.Cantidad) > p.Existencias)
                r.Agregar("Lineas", $"Existencias insuficientes de {p.Codigo} ({p.Nombre}): disponibles {p.Existencias:0.####}.");
        }
        return resultado;
    }

    private async Task<List<(LineaPreparadaBase, LineaEntrada)>> PrepararLineasNotaCreditoAsync(
        DocumentoTributario ccf, List<LineaNotaCreditoSolicitud> solicitadas, ResultadoValidacion r, CancellationToken ct)
    {
        var acreditables = await LineasAcreditablesAsync(ccf, ct);
        var solicitadoPorLinea = solicitadas.GroupBy(x => x.DetalleRelacionadoId).ToDictionary(g => g.Key, g => g.Sum(x => x.Cantidad));

        if (solicitadoPorLinea.Keys.Any(k => acreditables.All(a => a.Linea.Id != k)))
            r.Agregar("Lineas", "Una de las líneas indicadas no pertenece al comprobante relacionado.");

        var evaluacion = acreditables
            .Select(a => (a.Linea, a.YaAcreditado, solicitadoPorLinea.GetValueOrDefault(a.Linea.Id)))
            .ToList();
        r.Agregar(ReglasDocumento.ValidarCantidadesNotaCredito(evaluacion));

        var resultado = new List<(LineaPreparadaBase, LineaEntrada)>();
        foreach (var (linea, _, cantidad) in evaluacion.Where(e => e.Item3 > 0))
        {
            // Descuento proporcional a la cantidad acreditada para no acreditar más de lo cobrado.
            var descuento = Dinero.Redondear(linea.MontoDescuento * cantidad / linea.Cantidad);
            resultado.Add((new LineaPreparadaBase(linea.ProductoId, linea.Id, linea.Codigo, linea.Descripcion, linea.UnidadMedida),
                new LineaEntrada(cantidad, linea.PrecioUnitario, descuento)));
        }
        return resultado;
    }

    /// <summary>Líneas del CCF con la cantidad ya acreditada por notas de crédito vigentes.</summary>
    public async Task<List<LineaAcreditable>> LineasAcreditablesAsync(DocumentoTributario ccf, CancellationToken ct = default)
    {
        var detalles = ccf.Detalles.Count > 0
            ? ccf.Detalles.ToList()
            : await db.DetallesDocumento.AsNoTracking().Where(d => d.DocumentoId == ccf.Id).ToListAsync(ct);

        var acreditado = await db.DetallesDocumento.AsNoTracking()
            .Where(d => d.DetalleRelacionadoId != null
                        && d.Documento!.DocumentoRelacionadoId == ccf.Id
                        && d.Documento.Estado == EstadoDocumento.Emitido)
            .GroupBy(d => d.DetalleRelacionadoId!.Value)
            .Select(g => new { Id = g.Key, Cantidad = g.Sum(x => x.Cantidad) })
            .ToDictionaryAsync(x => x.Id, x => x.Cantidad, ct);

        return detalles.OrderBy(d => d.NumItem)
            .Select(d => new LineaAcreditable(d, acreditado.GetValueOrDefault(d.Id)))
            .ToList();
    }

    private Task<bool> TieneAnulacionVigenteAsync(int documentoId, CancellationToken ct) =>
        db.Anulaciones.AnyAsync(a => a.DocumentoId == documentoId && a.Estado != EstadoAnulacion.Rechazada, ct);

    public async Task<ResultadoEmision> EmitirAsync(SolicitudEmision s, Guid claveIdempotencia, CancellationToken ct = default)
    {
        if (claveIdempotencia == Guid.Empty) throw new ReglaNegocioException("La solicitud no es válida. Vuelva a revisar el documento.");

        var existente = await BuscarPorClaveAsync(claveIdempotencia, ct);
        if (existente is not null) return new ResultadoEmision(existente.Value, true, null);

        if (transmisor.Modo == ModoOperacion.Oficial)
            throw new ReglaNegocioException("El modo oficial no está implementado. Configure Facturacion:Modo = Academico.");

        var prep = await PrepararAsync(s, ct);
        var emisor = EmisorSnapshot.Desde(await db.ConfiguracionEmisor.AsNoTracking().FirstAsync(ct));
        var escritos = new List<string>();
        DocumentoTributario doc;
        EnvioCorreo envio;
        string json;
        var ahoraUtc = reloj.UtcNow;
        var local = reloj.ALocal(ahoraUtc);
        await AsegurarCorrelativoAsync(s.Tipo, local.Year, ct);

        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            try
            {
                var correlativo = await SiguienteCorrelativoAsync(s.Tipo, local.Year, ct);

                if (s.Tipo != TipoDte.NotaCredito)
                    await DescontarInventarioAsync(prep, ct);

                doc = ConstruirDocumento(prep, emisor, claveIdempotencia, ahoraUtc, local, correlativo);
                db.Documentos.Add(doc);
                await db.SaveChangesAsync(ct);

                // Archivos: JSON (validado antes de escribir) y PDF del mismo documento.
                var codigo = GeneradorDteJson.Codigo(doc.CodigoGeneracion);
                var carpeta = $"{local:yyyy}/{local:MM}";
                json = GeneradorDteJson.Generar(doc, emisor);
                var jsonBytes = new UTF8Encoding(false).GetBytes(json);
                VerificarJson(jsonBytes, codigo);
                var archivoJson = await almacen.GuardarNuevoAsync($"{carpeta}/{codigo}.json", jsonBytes, ct);
                escritos.Add(archivoJson.RutaRelativa);

                var pdf = generadorPdf.Generar(doc, emisor);
                VerificarPdf(pdf);
                var archivoPdf = await almacen.GuardarNuevoAsync($"{carpeta}/{codigo}.pdf", pdf, ct);
                escritos.Add(archivoPdf.RutaRelativa);

                doc.Archivos.Add(Archivo(PropositoArchivo.DocumentoJson, $"{codigo}.json", archivoJson, $"{TiposDteInfo.Codigo(doc.Tipo)}-v{doc.VersionEsquema}", ahoraUtc));
                doc.Archivos.Add(Archivo(PropositoArchivo.DocumentoPdf, $"{codigo}.pdf", archivoPdf, generadorPdf.Version, ahoraUtc));

                envio = new EnvioCorreo
                {
                    TipoDocumento = doc.Tipo,
                    Motivo = MotivoEnvio.Emision,
                    Destinatario = doc.ReceptorCorreo ?? "",
                    NumeroIntento = 1,
                    Estado = EstadoEnvio.Pendiente,
                    CreadoUtc = ahoraUtc,
                    Usuario = usuario.Nombre
                };
                doc.Envios.Add(envio);

                auditoria.Agregar("Documento emitido", nameof(DocumentoTributario), doc.Id.ToString(), ResultadoOperacion.Exito,
                    $"{TiposDteInfo.NombreCorto(doc.Tipo)} {doc.NumeroControl}, código {codigo}, total {doc.TotalPagar:0.00}");
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                foreach (var ruta in escritos)
                {
                    try { almacen.Eliminar(ruta); }
                    catch (Exception e) { logger.LogWarning(e, "No se pudo eliminar el archivo temporal {Ruta}", ruta); }
                }

                // Doble envío simultáneo: otra petición con la misma clave ya emitió el documento.
                if (ex is DbUpdateException && await BuscarPorClaveAsync(claveIdempotencia, CancellationToken.None) is { } id)
                    return new ResultadoEmision(id, true, null);

                if (ex is ReglaNegocioException)
                {
                    await auditoria.RegistrarAsync("Emisión rechazada", nameof(DocumentoTributario), null, ResultadoOperacion.Error, ex.Message, CancellationToken.None);
                    throw;
                }
                logger.LogError(ex, "Error al emitir documento {Tipo}", s.Tipo);
                await auditoria.RegistrarAsync("Emisión fallida", nameof(DocumentoTributario), null, ResultadoOperacion.Error,
                    $"Error interno ({ex.GetType().Name}). No se guardó ningún documento.", CancellationToken.None);
                throw;
            }
        }

        // Paso de transmisión: en modo académico no se envía nada a Hacienda; se deja constancia.
        try
        {
            var t = await transmisor.TransmitirDteAsync(doc, json, ct);
            await auditoria.RegistrarAsync(t.Simulado ? "Transmisión simulada (modo académico)" : "Transmisión a Hacienda",
                nameof(DocumentoTributario), doc.Id.ToString(), t.Procesado ? ResultadoOperacion.Exito : ResultadoOperacion.Advertencia, t.Mensaje, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error en el paso de transmisión del documento {Id}", doc.Id);
        }

        colaCorreo.Encolar(envio.Id);
        return new ResultadoEmision(doc.Id, false, envio.Id);
    }

    private async Task<int?> BuscarPorClaveAsync(Guid clave, CancellationToken ct)
    {
        var id = await db.Documentos.AsNoTracking().Where(d => d.ClaveIdempotencia == clave).Select(d => d.Id).FirstOrDefaultAsync(ct);
        return id == 0 ? null : id;
    }

    /// <summary>
    /// Crea la fila del correlativo del tipo y año si no existe, fuera de la transacción de emisión. Si otra
    /// emisión simultánea la crea primero, el índice único rechaza el duplicado y se usa la existente.
    /// </summary>
    private async Task AsegurarCorrelativoAsync(TipoDte tipo, int anio, CancellationToken ct)
    {
        if (await db.Correlativos.AnyAsync(c => c.Tipo == tipo && c.Anio == anio, ct)) return;
        db.Correlativos.Add(new Correlativo { Tipo = tipo, Anio = anio, Ultimo = 0 });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            if (!await db.Correlativos.AsNoTracking().AnyAsync(c => c.Tipo == tipo && c.Anio == anio, ct)) throw;
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private async Task<long> SiguienteCorrelativoAsync(TipoDte tipo, int anio, CancellationToken ct)
    {
        // Incremento atómico: el bloqueo de fila se mantiene hasta el fin de la transacción, así que dos
        // emisiones simultáneas nunca obtienen el mismo número.
        var filas = await db.Correlativos.Where(c => c.Tipo == tipo && c.Anio == anio)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.Ultimo, c => c.Ultimo + 1), ct);
        if (filas != 1) throw new InvalidOperationException("No se encontró el correlativo del tipo de documento.");
        return await db.Correlativos.AsNoTracking().Where(c => c.Tipo == tipo && c.Anio == anio).Select(c => c.Ultimo).FirstAsync(ct);
    }

    private async Task DescontarInventarioAsync(DocumentoPreparado prep, CancellationToken ct)
    {
        var porProducto = prep.Lineas.Where(l => l.ProductoId is not null)
            .GroupBy(l => l.ProductoId!.Value)
            .Select(g => (Id: g.Key, Cantidad: g.Sum(x => x.Calculo.Cantidad)));
        foreach (var (id, cantidad) in porProducto)
        {
            var controla = await db.Productos.AsNoTracking().Where(p => p.Id == id).Select(p => p.ControlaInventario).FirstAsync(ct);
            if (!controla) continue;
            var version = Guid.NewGuid();
            // UPDATE condicionado: solo descuenta si hay existencias suficientes en este momento.
            var filas = await db.Productos
                .Where(p => p.Id == id && p.Existencias >= cantidad)
                .ExecuteUpdateAsync(u => u.SetProperty(p => p.Existencias, p => p.Existencias - cantidad)
                                          .SetProperty(p => p.Version, version), ct);
            if (filas == 0)
                throw new ReglaNegocioException("Las existencias cambiaron mientras se emitía el documento y ya no son suficientes. Revise el documento.");
        }
    }

    private DocumentoTributario ConstruirDocumento(DocumentoPreparado p, EmisorSnapshot emisor, Guid clave, DateTime ahoraUtc, DateTime local, long correlativo)
    {
        var c = p.Cliente;
        var doc = new DocumentoTributario
        {
            Tipo = p.Tipo,
            CodigoGeneracion = Guid.NewGuid(),
            NumeroControl = $"DTE-{TiposDteInfo.Codigo(p.Tipo)}-{emisor.CodEstable}{emisor.CodPuntoVenta}-{correlativo:D15}",
            VersionEsquema = TiposDteInfo.VersionEsquema(p.Tipo),
            Ambiente = Catalogos.AmbientePruebas,
            Modo = transmisor.Modo,
            Estado = EstadoDocumento.Emitido,
            EmitidoUtc = ahoraUtc,
            FechaEmision = DateOnly.FromDateTime(local),
            HoraEmision = new TimeOnly(local.Hour, local.Minute, local.Second),
            ClienteId = c.Id,
            ReceptorTipoCliente = c.Tipo,
            ReceptorNombre = c.Nombre,
            ReceptorNombreComercial = c.NombreComercial,
            ReceptorTipoDocumento = c.TipoDocumento,
            ReceptorNumDocumento = c.NumeroDocumento,
            ReceptorNrc = c.Nrc,
            ReceptorCodActividad = c.CodActividad,
            ReceptorDescActividad = c.DescActividad,
            ReceptorDepartamento = c.Departamento,
            ReceptorMunicipio = c.Municipio,
            ReceptorComplemento = c.Complemento,
            ReceptorTelefono = c.Telefono,
            ReceptorCorreo = c.Correo,
            EmisorJson = emisor.Serializar(),
            CondicionOperacion = Catalogos.CondicionContado,
            FormaPago = p.FormaPago,
            TotalGravada = p.Resumen.TotalGravada,
            TotalDescuento = p.Resumen.TotalDescuento,
            SubTotal = p.Resumen.SubTotal,
            TotalIva = p.Resumen.TotalIva,
            MontoTotalOperacion = p.Resumen.MontoTotalOperacion,
            TotalPagar = p.Resumen.TotalPagar,
            TotalLetras = p.TotalLetras,
            DocumentoRelacionadoId = p.Relacionado?.Id,
            RelacionadoTipo = p.Relacionado?.Tipo,
            RelacionadoCodigoGeneracion = p.Relacionado?.CodigoGeneracion,
            RelacionadoFechaEmision = p.Relacionado?.FechaEmision,
            Observaciones = p.Observaciones,
            UsuarioEmisor = usuario.Nombre,
            ClaveIdempotencia = clave
        };
        foreach (var l in p.Lineas)
        {
            doc.Detalles.Add(new DetalleDocumento
            {
                NumItem = l.NumItem,
                ProductoId = l.ProductoId,
                DetalleRelacionadoId = l.DetalleRelacionadoId,
                Codigo = l.Codigo,
                Descripcion = l.Descripcion,
                UnidadMedida = l.UnidadMedida,
                Cantidad = l.Calculo.Cantidad,
                PrecioUnitario = l.Calculo.PrecioUnitario,
                MontoDescuento = l.Calculo.MontoDescuento,
                VentaGravada = l.Calculo.VentaGravada,
                IvaItem = l.Calculo.IvaItem
            });
        }
        return doc;
    }

    private static ArchivoGenerado Archivo(PropositoArchivo proposito, string nombre, ArchivoEscrito a, string version, DateTime utc) => new()
    {
        Proposito = proposito,
        NombreArchivo = nombre,
        RutaRelativa = a.RutaRelativa,
        TamanoBytes = a.TamanoBytes,
        Sha256 = a.Sha256,
        Version = version,
        GeneradoUtc = utc
    };

    /// <summary>Comprueba que el JSON es válido y corresponde al documento (mismo código de generación).</summary>
    public static void VerificarJson(byte[] contenido, string codigoGeneracion)
    {
        using var json = JsonDocument.Parse(contenido);
        var codigo = json.RootElement.GetProperty("identificacion").GetProperty("codigoGeneracion").GetString();
        if (codigo != codigoGeneracion) throw new InvalidOperationException("El JSON generado no corresponde al documento.");
    }

    public static void VerificarPdf(byte[] contenido)
    {
        if (contenido.Length < 100 || Encoding.ASCII.GetString(contenido, 0, 5) != "%PDF-")
            throw new InvalidOperationException("El PDF generado no es válido.");
    }
}

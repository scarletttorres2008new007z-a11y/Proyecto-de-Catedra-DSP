using Facturacion.Application;
using Facturacion.Application.Servicios;
using Facturacion.Domain;
using Facturacion.Domain.Reglas;
using Facturacion.Web.Infraestructura;
using Facturacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Facturacion.Web.Controllers;

[Authorize(Policy = Politicas.Personal)]
public sealed class DocumentosController(
    DocumentoService documentos,
    EmisionService emision,
    CorreoService correo,
    ClienteService clientes,
    ProductoService productos,
    ConfiguracionEmisorService emisor,
    AnulacionService anulaciones) : ControladorBase
{
    public async Task<IActionResult> Index(TipoDte? tipo, EstadoDocumento? estado, DateOnly? desde, DateOnly? hasta,
        string? busqueda, bool envioFallido = false, int pagina = 1)
    {
        ViewData["Filtro"] = new FiltroDocumentos(tipo, estado, desde, hasta, busqueda, envioFallido, pagina);
        return View(await documentos.ListarAsync(new FiltroDocumentos(tipo, estado, desde, hasta, busqueda, envioFallido, pagina)));
    }

    public async Task<IActionResult> Detalle(int id)
    {
        var d = await documentos.ObtenerDetalleAsync(id);
        if (d is null) return NotFound();
        var e = EmisorSnapshot.Deserializar(d.EmisorJson);
        var notas = await documentos.NotasCreditoDeAsync(id);
        var anulacionVigente = d.Anulaciones.Any(a => a.Estado != EstadoAnulacion.Rechazada);
        return View(new DetalleDocumentoVista
        {
            Documento = d,
            Emisor = new EmisorDatos(e.Nombre, e.NombreComercial, e.Nit, e.Nrc),
            NotasCredito = notas,
            PuedeEmitirNotaCredito = d.Tipo == TipoDte.ComprobanteCreditoFiscal && d.Estado == EstadoDocumento.Emitido && !anulacionVigente,
            Anulacion = User.IsInRole(Roles.Administrador) ? await anulaciones.ObtenerContextoAsync(id) : null
        });
    }

    /// <summary>Estado de los envíos de correo (consultado por la página de detalle mientras hay envíos pendientes).</summary>
    [HttpGet]
    public async Task<IActionResult> EstadoEnvios(int id)
    {
        var envios = await documentos.EnviosAsync(id);
        return Json(new { pendientes = envios.Count(e => e.Estado == EstadoEnvio.Pendiente) });
    }

    [HttpGet]
    public async Task<IActionResult> Emitir(TipoDte tipo = TipoDte.FacturaConsumidorFinal, int? clienteId = null)
    {
        if (tipo == TipoDte.NotaCredito) return RedirectToAction(nameof(NotaCredito));
        var s = new SolicitudEmision { Tipo = tipo, ClienteId = clienteId ?? 0 };
        s.Lineas.Add(new LineaSolicitud { Cantidad = 1 });
        return View("Emitir", await CrearVistaEmisionAsync(s));
    }

    private async Task<EmisionVista> CrearVistaEmisionAsync(SolicitudEmision s) => new()
    {
        Solicitud = s,
        Clientes = await clientes.ActivosParaAsync(s.Tipo),
        Productos = await productos.ActivosAsync(),
        EmisorConfigurado = await emisor.ObtenerAsync() is { } e && ConfiguracionEmisorService.Validar(e).EsValido
    };

    [HttpGet]
    public async Task<IActionResult> NotaCredito(int? ccfId)
    {
        var s = new SolicitudEmision { Tipo = TipoDte.NotaCredito, DocumentoRelacionadoId = ccfId };
        return View("NotaCredito", await CrearVistaNotaCreditoAsync(s));
    }

    private async Task<NotaCreditoVista> CrearVistaNotaCreditoAsync(SolicitudEmision s)
    {
        var vista = new NotaCreditoVista { Solicitud = s };
        if (s.DocumentoRelacionadoId is { } id)
        {
            vista.Ccf = await documentos.ObtenerDetalleAsync(id);
            if (vista.Ccf is { Tipo: TipoDte.ComprobanteCreditoFiscal })
            {
                vista.Lineas = await emision.LineasAcreditablesAsync(vista.Ccf);
                if (s.LineasNotaCredito.Count == 0)
                    s.LineasNotaCredito = vista.Lineas.Select(l => new LineaNotaCreditoSolicitud { DetalleRelacionadoId = l.Linea.Id }).ToList();
            }
            else vista.Ccf = null;
        }
        if (vista.Ccf is null) vista.CcfDisponibles = await documentos.CreditosFiscalesVigentesAsync();
        return vista;
    }

    /// <summary>Vuelve al formulario con los datos enviados (botón "Modificar" en la revisión).</summary>
    [HttpPost]
    public async Task<IActionResult> Modificar(SolicitudEmision solicitud) =>
        solicitud.Tipo == TipoDte.NotaCredito
            ? View("NotaCredito", await CrearVistaNotaCreditoAsync(solicitud))
            : View("Emitir", await CrearVistaEmisionAsync(solicitud));

    /// <summary>Calcula en el servidor y muestra la revisión previa. No guarda nada.</summary>
    [HttpPost]
    public async Task<IActionResult> Revisar(SolicitudEmision solicitud)
    {
        solicitud.Lineas = solicitud.Lineas.Where(l => l.ProductoId > 0).ToList();
        try
        {
            var preparado = await emision.PrepararAsync(solicitud);
            return View("Revisar", new RevisionVista { Solicitud = solicitud, Preparado = preparado, ClaveIdempotencia = Guid.NewGuid() });
        }
        catch (ReglaNegocioException ex)
        {
            AgregarErrores(ex, "Solicitud");
            return await Modificar(ConLineaVacia(solicitud));
        }
    }

    /// <summary>Emite el documento. La clave de idempotencia evita duplicados por doble clic o reintentos.</summary>
    [HttpPost]
    public async Task<IActionResult> Confirmar(SolicitudEmision solicitud, Guid claveIdempotencia)
    {
        try
        {
            var r = await emision.EmitirAsync(solicitud, claveIdempotencia);
            if (r.YaExistia) Aviso("Esta solicitud ya había sido procesada: se muestra el documento emitido (no se emitió otro).");
            else Exito("Documento emitido y archivos JSON y PDF generados. El envío por correo se está procesando.");
            return RedirectToAction(nameof(Detalle), new { id = r.DocumentoId });
        }
        catch (ReglaNegocioException ex)
        {
            AgregarErrores(ex, "Solicitud");
            return await Modificar(ConLineaVacia(solicitud));
        }
    }

    private static SolicitudEmision ConLineaVacia(SolicitudEmision s)
    {
        if (s.Tipo != TipoDte.NotaCredito && s.Lineas.Count == 0) s.Lineas.Add(new LineaSolicitud { Cantidad = 1 });
        return s;
    }

    /// <summary>Descarga controlada: solo usuarios autenticados y solo archivos registrados para ese documento.</summary>
    [HttpGet]
    public async Task<IActionResult> Archivo(int id, int archivoId)
    {
        var a = await documentos.ObtenerArchivoAsync(id, archivoId);
        if (a is null) return NotFound();
        return File(a.Contenido, a.TipoContenido, a.Nombre);
    }

    /// <summary>Reenvía los mismos archivos ya generados. No emite un documento nuevo.</summary>
    [HttpPost]
    public async Task<IActionResult> Reenviar(int id, string? destinatario)
    {
        try
        {
            var envio = await correo.SolicitarEnvioAsync(id, MotivoEnvio.Reenvio, destinatario);
            Exito($"Reenvío registrado (intento {envio.NumeroIntento}). Se están enviando los mismos archivos del documento.");
        }
        catch (ReglaNegocioException ex)
        {
            Error(ex.Message);
        }
        return RedirectToAction(nameof(Detalle), new { id });
    }
}

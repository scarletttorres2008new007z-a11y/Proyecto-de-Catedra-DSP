using Facturacion.Application.Servicios;
using Facturacion.Domain;
using Facturacion.Domain.Reglas;
using Facturacion.Web.Infraestructura;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Facturacion.Web.Controllers;

[Authorize(Policy = Politicas.SoloAdministrador)]
public sealed class AnulacionesController(AnulacionService anulaciones) : ControladorBase
{
    public async Task<IActionResult> Index(EstadoAnulacion? estado, int pagina = 1)
    {
        ViewData["Estado"] = estado;
        return View(await anulaciones.ListarAsync(estado, pagina));
    }

    [HttpGet]
    public async Task<IActionResult> Solicitar(int documentoId)
    {
        var ctx = await anulaciones.ObtenerContextoAsync(documentoId);
        if (ctx is null) return NotFound();
        ViewData["Contexto"] = ctx;
        return View(new SolicitudAnulacion { DocumentoId = documentoId, Tipo = TipoAnulacion.RescindirOperacion, TipoDocResponsable = Catalogos.DocDui, TipoDocSolicita = Catalogos.DocDui });
    }

    [HttpPost]
    public async Task<IActionResult> Solicitar(SolicitudAnulacion s)
    {
        try
        {
            var r = await anulaciones.SolicitarAsync(s);
            if (r.Estado == EstadoAnulacion.ProcesadaSimulada)
                Exito("Anulación procesada en modo académico (simulación). El documento quedó anulado y se está notificando al cliente.");
            else if (r.Estado == EstadoAnulacion.ProcesadaOficial)
                Exito("Anulación procesada. Se está notificando al cliente.");
            else
                Error($"La anulación no fue procesada: {r.Mensaje}");
            return RedirectToAction("Detalle", "Documentos", new { id = s.DocumentoId });
        }
        catch (ReglaNegocioException ex)
        {
            AgregarErrores(ex);
            var ctx = await anulaciones.ObtenerContextoAsync(s.DocumentoId);
            if (ctx is null) return NotFound();
            ViewData["Contexto"] = ctx;
            return View(s);
        }
    }

    [HttpPost]
    public async Task<IActionResult> ReenviarNotificacion(int id, int documentoId, string? destinatario)
    {
        try
        {
            var envio = await anulaciones.ReenviarNotificacionAsync(id, destinatario);
            Exito($"Notificación de anulación registrada (intento {envio.NumeroIntento}).");
        }
        catch (ReglaNegocioException ex)
        {
            Error(ex.Message);
        }
        return RedirectToAction("Detalle", "Documentos", new { id = documentoId });
    }
}

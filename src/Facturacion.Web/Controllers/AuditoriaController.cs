using Facturacion.Application.Servicios;
using Facturacion.Domain;
using Facturacion.Domain.Reglas;
using Facturacion.Web.Infraestructura;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Facturacion.Web.Controllers;

[Authorize(Policy = Politicas.SoloAdministrador)]
public sealed class AuditoriaController(AuditoriaConsultaService auditoria) : ControladorBase
{
    public async Task<IActionResult> Index(DateOnly? desde, DateOnly? hasta, string? accion, ResultadoOperacion? resultado,
        string? identificador, string? usuario, int pagina = 1)
    {
        var f = new FiltroAuditoria(desde, hasta, accion, resultado, identificador, usuario, pagina);
        ViewData["Filtro"] = f;
        ViewData["Acciones"] = await auditoria.AccionesAsync();
        return View(await auditoria.ListarAsync(f));
    }
}

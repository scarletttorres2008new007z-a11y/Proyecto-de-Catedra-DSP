using Facturacion.Application.Abstracciones;
using Facturacion.Application.Servicios;
using Facturacion.Domain;
using Facturacion.Domain.Reglas;
using Facturacion.Web.Infraestructura;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Facturacion.Web.Controllers;

[Authorize(Policy = Politicas.SoloAdministrador)]
public sealed class RespaldosController(RespaldoService respaldos, IReloj reloj) : ControladorBase
{
    public async Task<IActionResult> Index(int pagina = 1)
    {
        ViewData["Motor"] = respaldos.Motor;
        ViewData["Directorio"] = respaldos.Directorio;
        return View(await respaldos.ListarAsync(pagina));
    }

    [HttpPost]
    public async Task<IActionResult> Crear()
    {
        var r = await respaldos.CrearAsync();
        if (r.Estado == EstadoRespaldo.Exitoso) Exito($"Backup ({reloj.ALocal(r.IniciadoUtc):dd/MM/yyyy}) creado: {r.NombreArchivo}. {r.Mensaje}");
        else Error($"La copia de seguridad falló: {r.Mensaje}");
        return RedirectToAction(nameof(Index));
    }
}

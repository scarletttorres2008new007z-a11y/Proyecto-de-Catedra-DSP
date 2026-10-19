using Facturacion.Application.Abstracciones;
using Facturacion.Application.Servicios;
using Facturacion.Web.Infraestructura;
using Microsoft.AspNetCore.Mvc;

namespace Facturacion.Web.Controllers;

public sealed class InicioController(PanelService panel, IReloj reloj) : ControladorBase
{
    /// <param name="periodo">hoy, semana, mes (por defecto) o personalizado.</param>
    public async Task<IActionResult> Index(string? periodo, DateOnly? desde, DateOnly? hasta)
    {
        var hoy = DateOnly.FromDateTime(reloj.AhoraLocal);
        periodo ??= "mes";
        (DateOnly d, DateOnly h) = periodo switch
        {
            "hoy" => (hoy, hoy),
            "semana" => (hoy.AddDays(-6), hoy),
            "personalizado" when desde is not null && hasta is not null && desde <= hasta => (desde.Value, hasta.Value),
            _ => (new DateOnly(hoy.Year, hoy.Month, 1), hoy)
        };
        if (periodo == "personalizado" && (desde is null || hasta is null || desde > hasta))
        {
            Aviso("Indique un rango de fechas válido; se muestra el mes actual.");
            periodo = "mes";
        }
        ViewData["Periodo"] = periodo;
        return View(await panel.ObtenerAsync(d, h));
    }
}

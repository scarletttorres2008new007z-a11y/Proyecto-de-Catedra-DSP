using Facturacion.Application.Configuracion;
using Facturacion.Application.Servicios;
using Facturacion.Domain.Reglas;
using Facturacion.Infrastructure.Correo;
using Facturacion.Web.Infraestructura;
using Facturacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Facturacion.Web.Controllers;

public sealed record ResumenConfiguracion(OpcionesFacturacion Facturacion, OpcionesAnulacion Anulaciones, string SmtpServidor, int SmtpPuerto,
    string SmtpSeguridad, string SmtpRemitente, bool SmtpCredencialesConfiguradas);

[Authorize(Policy = Politicas.SoloAdministrador)]
public sealed class ConfiguracionController(
    ConfiguracionEmisorService emisor,
    IOptions<OpcionesFacturacion> facturacion,
    IOptions<OpcionesAnulacion> anulacion,
    IOptionsMonitor<OpcionesSmtp> smtp) : ControladorBase
{
    public async Task<IActionResult> Index()
    {
        var actual = await emisor.ObtenerAsync();
        ViewData["Resumen"] = Resumen();
        ViewData["Modificado"] = actual is null ? null : $"{actual.ModificadoUtc:yyyy-MM-dd HH:mm} UTC por {actual.ModificadoPor}";
        return View(actual is null ? new EmisorFormulario() : EmisorFormulario.Desde(actual));
    }

    /// <summary>Solo muestra si hay credenciales SMTP, nunca su valor.</summary>
    private ResumenConfiguracion Resumen()
    {
        var s = smtp.CurrentValue;
        return new ResumenConfiguracion(facturacion.Value, anulacion.Value, s.Servidor, s.Puerto, s.Seguridad, s.RemitenteCorreo,
            !string.IsNullOrEmpty(s.Usuario) && !string.IsNullOrEmpty(s.Contrasena));
    }

    [HttpPost]
    public async Task<IActionResult> Index(EmisorFormulario f)
    {
        try
        {
            await emisor.GuardarAsync(f.AEntidad());
            Exito("Datos del emisor guardados. Se aplicarán a los documentos que se emitan desde ahora.");
            return RedirectToAction(nameof(Index));
        }
        catch (ReglaNegocioException ex)
        {
            AgregarErrores(ex);
            ViewData["Resumen"] = Resumen();
            return View(f);
        }
    }
}

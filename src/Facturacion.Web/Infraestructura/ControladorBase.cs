using Facturacion.Domain.Reglas;
using Microsoft.AspNetCore.Mvc;

namespace Facturacion.Web.Infraestructura;

public abstract class ControladorBase : Controller
{
    protected void Exito(string mensaje) => TempData["Exito"] = mensaje;
    protected void Aviso(string mensaje) => TempData["Aviso"] = mensaje;
    protected void Error(string mensaje) => TempData["Error"] = mensaje;

    /// <summary>Copia los errores de negocio al ModelState (con prefijo opcional para los nombres de campo).</summary>
    protected void AgregarErrores(ReglaNegocioException ex, string prefijo = "")
    {
        foreach (var e in ex.Resultado.Errores)
        {
            var campo = string.IsNullOrEmpty(e.Campo) ? "" : (prefijo.Length > 0 ? $"{prefijo}.{e.Campo}" : e.Campo);
            ModelState.AddModelError(campo, e.Mensaje);
        }
    }

    /// <summary>Evita redirecciones abiertas: solo URL locales.</summary>
    protected IActionResult VolverA(string? url, string accion, object? valores = null) =>
        !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) ? LocalRedirect(url) : RedirectToAction(accion, valores);
}

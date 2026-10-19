using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Facturacion.Web.Controllers;

/// <summary>Páginas de error controladas: no muestran detalles internos, solo un identificador de seguimiento.</summary>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route("Error")]
public sealed class ErrorController : Controller
{
    [Route("")]
    public IActionResult Index()
    {
        Response.StatusCode = 500;
        ViewData["Codigo"] = 500;
        ViewData["Seguimiento"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View("Error");
    }

    [Route("{codigo:int}")]
    public IActionResult Codigo(int codigo)
    {
        ViewData["Codigo"] = codigo;
        ViewData["Seguimiento"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View("Error");
    }
}

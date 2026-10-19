using Facturacion.Application.Servicios;
using Facturacion.Domain.Reglas;
using Facturacion.Web.Infraestructura;
using Facturacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Facturacion.Web.Controllers;

[Authorize(Policy = Politicas.Personal)]
public sealed class ProductosController(ProductoService productos) : ControladorBase
{
    public async Task<IActionResult> Index(string? busqueda, string? estado, bool inventarioBajo = false, int pagina = 1)
    {
        bool? activo = estado switch { "activos" => true, "inactivos" => false, _ => null };
        ViewData["Busqueda"] = busqueda;
        ViewData["Estado"] = estado;
        ViewData["InventarioBajo"] = inventarioBajo;
        return View(await productos.ListarAsync(new FiltroProductos(busqueda, activo, inventarioBajo, pagina)));
    }

    public async Task<IActionResult> Detalle(int id)
    {
        var p = await productos.ObtenerAsync(id);
        return p is null ? NotFound() : View(p);
    }

    [HttpGet]
    public IActionResult Crear() => View("Formulario", new ProductoFormulario());

    [HttpPost]
    public async Task<IActionResult> Crear(ProductoFormulario f)
    {
        if (!ModelState.IsValid) return View("Formulario", f);
        try
        {
            var p = await productos.CrearAsync(f.AEntidad());
            Exito($"Producto {p.Codigo} registrado.");
            return RedirectToAction(nameof(Detalle), new { id = p.Id });
        }
        catch (ReglaNegocioException ex)
        {
            AgregarErrores(ex);
            return View("Formulario", f);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var p = await productos.ObtenerAsync(id);
        return p is null ? NotFound() : View("Formulario", ProductoFormulario.Desde(p));
    }

    [HttpPost]
    public async Task<IActionResult> Editar(int id, ProductoFormulario f)
    {
        if (id != f.Id) return BadRequest();
        if (!ModelState.IsValid) return View("Formulario", f);
        try
        {
            await productos.ActualizarAsync(f.AEntidad());
            Exito("Producto actualizado. Los documentos ya emitidos conservan el precio con que se emitieron.");
            return RedirectToAction(nameof(Detalle), new { id });
        }
        catch (ReglaNegocioException ex)
        {
            AgregarErrores(ex);
            return View("Formulario", f);
        }
    }

    [HttpPost]
    public async Task<IActionResult> CambiarEstado(int id, bool activo)
    {
        try
        {
            await productos.CambiarEstadoAsync(id, activo);
            Exito(activo ? "Producto activado." : "Producto desactivado. Ya no aparecerá en la emisión.");
        }
        catch (ReglaNegocioException ex)
        {
            Error(ex.Message);
        }
        return RedirectToAction(nameof(Detalle), new { id });
    }
}

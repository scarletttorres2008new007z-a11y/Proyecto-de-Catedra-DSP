using Facturacion.Application.Servicios;
using Facturacion.Domain;
using Facturacion.Domain.Reglas;
using Facturacion.Web.Infraestructura;
using Facturacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Facturacion.Web.Controllers;

[Authorize(Policy = Politicas.Personal)]
public sealed class ClientesController(ClienteService clientes) : ControladorBase
{
    public async Task<IActionResult> Index(string? busqueda, TipoCliente? tipo, string? estado, int pagina = 1)
    {
        bool? activo = estado switch { "activos" => true, "inactivos" => false, _ => null };
        ViewData["Busqueda"] = busqueda;
        ViewData["Tipo"] = tipo;
        ViewData["Estado"] = estado;
        return View(await clientes.ListarAsync(new FiltroClientes(busqueda, tipo, activo, pagina)));
    }

    public async Task<IActionResult> Detalle(int id)
    {
        var c = await clientes.ObtenerAsync(id);
        if (c is null) return NotFound();
        ViewData["Historial"] = await clientes.HistorialAsync(id);
        return View(c);
    }

    [HttpGet]
    public IActionResult Crear(TipoCliente tipo = TipoCliente.ConsumidorFinal, string? volverA = null) =>
        View("Formulario", new ClienteFormulario { Tipo = tipo, VolverA = volverA });

    [HttpPost]
    public async Task<IActionResult> Crear(ClienteFormulario f)
    {
        if (!ModelState.IsValid) return View("Formulario", f);
        try
        {
            var c = await clientes.CrearAsync(f.AEntidad());
            Exito($"Cliente \"{c.Nombre}\" registrado.");
            if (!string.IsNullOrEmpty(f.VolverA) && Url.IsLocalUrl(f.VolverA))
            {
                var separador = f.VolverA.Contains('?') ? "&" : "?";
                return LocalRedirect($"{f.VolverA}{separador}clienteId={c.Id}");
            }
            return RedirectToAction(nameof(Detalle), new { id = c.Id });
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
        var c = await clientes.ObtenerAsync(id);
        return c is null ? NotFound() : View("Formulario", ClienteFormulario.Desde(c));
    }

    [HttpPost]
    public async Task<IActionResult> Editar(int id, ClienteFormulario f)
    {
        if (id != f.Id) return BadRequest();
        if (!ModelState.IsValid) return View("Formulario", f);
        try
        {
            await clientes.ActualizarAsync(f.AEntidad());
            Exito("Cliente actualizado. Los documentos ya emitidos conservan los datos con que se emitieron.");
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
            await clientes.CambiarEstadoAsync(id, activo);
            Exito(activo ? "Cliente activado." : "Cliente desactivado. Ya no aparecerá en la emisión.");
        }
        catch (ReglaNegocioException ex)
        {
            Error(ex.Message);
        }
        return RedirectToAction(nameof(Detalle), new { id });
    }
}

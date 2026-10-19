using Facturacion.Application;
using Facturacion.Application.Abstracciones;
using Facturacion.Domain;
using Facturacion.Infrastructure.Identidad;
using Facturacion.Web.Infraestructura;
using Facturacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Web.Controllers;

public sealed record UsuarioFila(string Id, string Nombre, string Correo, string Rol, bool Activo, bool Bloqueado);

[Authorize(Policy = Politicas.SoloAdministrador)]
public sealed class UsuariosController(UserManager<UsuarioAplicacion> usuarios, IAuditoria auditoria, IReloj reloj) : ControladorBase
{
    public async Task<IActionResult> Index()
    {
        var lista = await usuarios.Users.OrderBy(u => u.NombreCompleto).ToListAsync();
        var filas = new List<UsuarioFila>();
        foreach (var u in lista)
        {
            var roles = await usuarios.GetRolesAsync(u);
            filas.Add(new UsuarioFila(u.Id, u.NombreCompleto, u.Email ?? "", string.Join(", ", roles), u.Activo,
                u.LockoutEnd is { } fin && fin > DateTimeOffset.UtcNow));
        }
        return View(filas);
    }

    [HttpGet]
    public IActionResult Crear() => View(new UsuarioFormulario());

    [HttpPost]
    public async Task<IActionResult> Crear(UsuarioFormulario f)
    {
        if (!Roles.Todos.Contains(f.Rol)) ModelState.AddModelError(nameof(f.Rol), "Rol no válido.");
        if (!ModelState.IsValid) return View(f);

        var u = new UsuarioAplicacion
        {
            UserName = f.Correo.Trim(), Email = f.Correo.Trim(), EmailConfirmed = true,
            NombreCompleto = f.NombreCompleto.Trim(), CreadoUtc = reloj.UtcNow
        };
        var r = await usuarios.CreateAsync(u, f.Contrasena);
        if (!r.Succeeded)
        {
            foreach (var e in r.Errors) ModelState.AddModelError("", e.Description);
            return View(f);
        }
        await usuarios.AddToRoleAsync(u, f.Rol);
        await auditoria.RegistrarAsync("Usuario creado", "Usuario", u.Id, ResultadoOperacion.Exito, $"{u.Email} ({f.Rol})");
        Exito($"Usuario {u.Email} creado con rol {f.Rol}. Comunique la contraseña inicial por un medio seguro.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> CambiarEstado(string id, bool activo)
    {
        var u = await usuarios.FindByIdAsync(id);
        if (u is null) return NotFound();
        if (u.Id == usuarios.GetUserId(User))
        {
            Error("No puede desactivar su propio usuario.");
            return RedirectToAction(nameof(Index));
        }
        u.Activo = activo;
        await usuarios.UpdateAsync(u);
        // Desactivar también invalida las sesiones abiertas.
        await usuarios.UpdateSecurityStampAsync(u);
        await auditoria.RegistrarAsync(activo ? "Usuario activado" : "Usuario desactivado", "Usuario", u.Id, ResultadoOperacion.Exito, u.Email);
        Exito(activo ? "Usuario activado." : "Usuario desactivado.");
        return RedirectToAction(nameof(Index));
    }
}

using System.ComponentModel.DataAnnotations;
using Facturacion.Application.Abstracciones;
using Facturacion.Domain;
using Facturacion.Infrastructure.Identidad;
using Facturacion.Web.Infraestructura;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Facturacion.Web.Controllers;

public sealed class InicioSesionModelo
{
    [Required(ErrorMessage = "Ingrese su correo.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
    [Display(Name = "Correo electrónico")]
    public string Correo { get; set; } = "";

    [Required(ErrorMessage = "Ingrese su contraseña.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Contrasena { get; set; } = "";

    public string? VolverA { get; set; }
}

public sealed class CambioContrasenaModelo
{
    [Required(ErrorMessage = "Ingrese su contraseña actual.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña actual")]
    public string Actual { get; set; } = "";

    [Required(ErrorMessage = "Ingrese la nueva contraseña.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nueva contraseña")]
    public string Nueva { get; set; } = "";

    [Compare(nameof(Nueva), ErrorMessage = "La confirmación no coincide.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar nueva contraseña")]
    public string Confirmacion { get; set; } = "";
}

public sealed class CuentaController(
    SignInManager<UsuarioAplicacion> signIn,
    UserManager<UsuarioAplicacion> usuarios,
    IAuditoria auditoria) : ControladorBase
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult IniciarSesion(string? volverA)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Inicio");
        return View(new InicioSesionModelo { VolverA = volverA ?? Request.Query["ReturnUrl"] });
    }

    [AllowAnonymous]
    [HttpPost]
    [EnableRateLimiting("inicio-sesion")]
    public async Task<IActionResult> IniciarSesion(InicioSesionModelo m)
    {
        if (!ModelState.IsValid) return View(m);

        var usuario = await usuarios.FindByEmailAsync(m.Correo.Trim());
        if (usuario is null || !usuario.Activo)
        {
            await auditoria.RegistrarAsync("Inicio de sesión fallido", "Usuario", null, ResultadoOperacion.Error, "Credenciales no válidas");
            ModelState.AddModelError("", "Correo o contraseña incorrectos.");
            return View(m);
        }

        var r = await signIn.PasswordSignInAsync(usuario, m.Contrasena, isPersistent: false, lockoutOnFailure: true);
        if (r.Succeeded)
        {
            await auditoria.RegistrarAsync("Inicio de sesión", "Usuario", usuario.Id, ResultadoOperacion.Exito, usuario.Email);
            return !string.IsNullOrEmpty(m.VolverA) && Url.IsLocalUrl(m.VolverA)
                ? LocalRedirect(m.VolverA)
                : RedirectToAction("Index", "Inicio");
        }

        await auditoria.RegistrarAsync("Inicio de sesión fallido", "Usuario", usuario.Id,
            ResultadoOperacion.Error, r.IsLockedOut ? "Cuenta bloqueada temporalmente" : "Contraseña incorrecta");
        ModelState.AddModelError("", r.IsLockedOut
            ? "La cuenta está bloqueada temporalmente por intentos fallidos. Intente más tarde."
            : "Correo o contraseña incorrectos.");
        return View(m);
    }

    [HttpPost]
    public async Task<IActionResult> CerrarSesion()
    {
        await auditoria.RegistrarAsync("Cierre de sesión", "Usuario", null, ResultadoOperacion.Exito);
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(IniciarSesion));
    }

    [AllowAnonymous]
    public IActionResult AccesoDenegado() => View();

    [HttpGet]
    public IActionResult CambiarContrasena() => View(new CambioContrasenaModelo());

    [HttpPost]
    public async Task<IActionResult> CambiarContrasena(CambioContrasenaModelo m)
    {
        if (!ModelState.IsValid) return View(m);
        var usuario = await usuarios.GetUserAsync(User);
        if (usuario is null) return RedirectToAction(nameof(IniciarSesion));
        var r = await usuarios.ChangePasswordAsync(usuario, m.Actual, m.Nueva);
        if (!r.Succeeded)
        {
            foreach (var e in r.Errors) ModelState.AddModelError("", e.Description);
            return View(m);
        }
        await signIn.RefreshSignInAsync(usuario);
        await auditoria.RegistrarAsync("Contraseña cambiada", "Usuario", usuario.Id, ResultadoOperacion.Exito);
        Exito("La contraseña se cambió correctamente.");
        return RedirectToAction("Index", "Inicio");
    }
}

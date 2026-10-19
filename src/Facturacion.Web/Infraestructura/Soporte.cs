using System.Security.Claims;
using Facturacion.Application.Abstracciones;
using Microsoft.AspNetCore.Identity;

namespace Facturacion.Web.Infraestructura;

public static class Politicas
{
    public const string SoloAdministrador = "SoloAdministrador";
    public const string Personal = "Personal";
}

/// <summary>Usuario de la petición HTTP actual; "sistema" para procesos en segundo plano.</summary>
public sealed class UsuarioActualHttp(IHttpContextAccessor accesor) : IUsuarioActual
{
    public string Nombre
    {
        get
        {
            var usuario = accesor.HttpContext?.User;
            return usuario?.Identity?.IsAuthenticated == true
                ? usuario.FindFirstValue(ClaimTypes.Name) ?? "desconocido"
                : "sistema";
        }
    }
}

/// <summary>Encabezados de seguridad: sin incrustación en marcos, sin detección de tipo MIME y CSP restrictiva.</summary>
public sealed class EncabezadosSeguridad(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext ctx)
    {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "same-origin";
        h["Content-Security-Policy"] =
            "default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; form-action 'self'; frame-ancestors 'none'; base-uri 'self'";
        h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        return next(ctx);
    }
}

/// <summary>Mensajes de ASP.NET Core Identity en español.</summary>
public sealed class DescriptorErroresIdentidad : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) => new() { Code = nameof(PasswordTooShort), Description = $"La contraseña debe tener al menos {length} caracteres." };
    public override IdentityError PasswordRequiresDigit() => new() { Code = nameof(PasswordRequiresDigit), Description = "La contraseña debe incluir al menos un dígito." };
    public override IdentityError PasswordRequiresLower() => new() { Code = nameof(PasswordRequiresLower), Description = "La contraseña debe incluir al menos una letra minúscula." };
    public override IdentityError PasswordRequiresUpper() => new() { Code = nameof(PasswordRequiresUpper), Description = "La contraseña debe incluir al menos una letra mayúscula." };
    public override IdentityError PasswordRequiresNonAlphanumeric() => new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "La contraseña debe incluir al menos un símbolo." };
    public override IdentityError DuplicateEmail(string email) => new() { Code = nameof(DuplicateEmail), Description = "Ya existe un usuario con ese correo." };
    public override IdentityError DuplicateUserName(string userName) => new() { Code = nameof(DuplicateUserName), Description = "Ya existe un usuario con ese nombre de usuario." };
    public override IdentityError InvalidEmail(string? email) => new() { Code = nameof(InvalidEmail), Description = "El correo no es válido." };
    public override IdentityError PasswordMismatch() => new() { Code = nameof(PasswordMismatch), Description = "La contraseña actual no es correcta." };
}

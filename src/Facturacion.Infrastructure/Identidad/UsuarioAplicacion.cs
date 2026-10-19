using Microsoft.AspNetCore.Identity;

namespace Facturacion.Infrastructure.Identidad;

/// <summary>Usuario del personal autorizado. Las contraseñas se guardan como hash (ASP.NET Core Identity).</summary>
public class UsuarioAplicacion : IdentityUser
{
    public string NombreCompleto { get; set; } = "";
    public bool Activo { get; set; } = true;
    public DateTime CreadoUtc { get; set; }
}

using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Facturacion.Application;
using Facturacion.Application.Abstracciones;
using Facturacion.Infrastructure.Identidad;
using Facturacion.Tests.Soporte;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Facturacion.Tests.Web;

/// <summary>
/// Aplicación web completa en memoria con una base SQLite temporal propia y correo falso. Los usuarios son
/// sintéticos y sus contraseñas se generan al azar en cada ejecución (no hay contraseñas en el código).
/// </summary>
public partial class FabricaWeb : WebApplicationFactory<Program>
{
    /// <summary>Intentos de inicio de sesión por minuto: alto para que las pruebas no choquen con el límite.</summary>
    protected virtual int IntentosPorMinuto => 1000;

    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "facturacion-pruebas-web", Guid.NewGuid().ToString("N"));
    public string ContrasenaAdmin { get; } = Generar();
    public string ContrasenaFacturador { get; } = Generar();
    public const string CorreoAdmin = "admin.prueba@ejemplo.test";
    public const string CorreoFacturador = "facturador.prueba@ejemplo.test";
    public EnviadorCorreoFalso Correo { get; } = new();

    private static string Generar() => "Pr1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_carpeta);
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Facturacion", $"Data Source={Path.Combine(_carpeta, "web.db")};Pooling=False");
        builder.UseSetting("BaseDatos:Proveedor", "Sqlite");
        builder.UseSetting("BaseDatos:MigrarAlIniciar", "true");
        builder.UseSetting("Correo:DeshabilitarTrabajador", "true");
        builder.UseSetting("Almacenamiento:RutaDocumentos", Path.Combine(_carpeta, "Documentos"));
        builder.UseSetting("Respaldos:Directorio", Path.Combine(_carpeta, "Respaldos"));
        builder.UseSetting("Seguridad:IntentosInicioSesionPorMinuto", IntentosPorMinuto.ToString());
        builder.ConfigureServices(s => s.AddSingleton<IEnviadorCorreo>(Correo));
    }

    public async Task SembrarUsuariosAsync()
    {
        using var scope = Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<UsuarioAplicacion>>();
        async Task Crear(string correo, string contrasena, string rol)
        {
            if (await um.FindByEmailAsync(correo) is not null) return;
            var u = new UsuarioAplicacion { UserName = correo, Email = correo, NombreCompleto = $"PRUEBA {rol}", Activo = true, EmailConfirmed = true };
            var r = await um.CreateAsync(u, contrasena);
            if (!r.Succeeded) throw new InvalidOperationException(string.Join(" ", r.Errors.Select(e => e.Description)));
            await um.AddToRoleAsync(u, rol);
        }
        await Crear(CorreoAdmin, ContrasenaAdmin, Roles.Administrador);
        await Crear(CorreoFacturador, ContrasenaFacturador, Roles.Facturador);
    }

    public HttpClient Cliente() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true
    });

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();

    public static async Task<string> TokenAsync(HttpClient c, string url)
    {
        var html = await c.GetStringAsync(url);
        return WebUtility.HtmlDecode(TokenRegex().Match(html).Groups[1].Value);
    }

    public static async Task<HttpResponseMessage> IniciarSesionAsync(HttpClient c, string correo, string contrasena)
    {
        var token = await TokenAsync(c, "/Cuenta/IniciarSesion");
        return await c.PostAsync("/Cuenta/IniciarSesion", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Correo"] = correo, ["Contrasena"] = contrasena, ["__RequestVerificationToken"] = token
        }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_carpeta, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

/// <summary>Fábrica con el límite real de 10 intentos por minuto.</summary>
public sealed class FabricaWebLimiteReal : FabricaWeb
{
    protected override int IntentosPorMinuto => 10;
}

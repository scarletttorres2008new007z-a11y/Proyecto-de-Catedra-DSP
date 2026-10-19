using System.Net;
using Facturacion.Application.Abstracciones;
using Facturacion.Infrastructure.Identidad;
using Facturacion.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Facturacion.Tests.Web;

public class SeguridadWebTests : IClassFixture<FabricaWeb>, IAsyncLifetime
{
    private readonly FabricaWeb _f;
    public SeguridadWebTests(FabricaWeb f) => _f = f;
    public Task InitializeAsync() => _f.SembrarUsuariosAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("/")]
    [InlineData("/Documentos")]
    [InlineData("/Clientes")]
    [InlineData("/Respaldos")]
    [InlineData("/Auditoria")]
    public async Task Paginas_privadas_redirigen_al_inicio_de_sesion(string url)
    {
        var r = await _f.Cliente().GetAsync(url);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/Cuenta/IniciarSesion", r.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Respuestas_incluyen_encabezados_de_seguridad()
    {
        var r = await _f.Cliente().GetAsync("/Cuenta/IniciarSesion");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("default-src 'self'", r.Headers.GetValues("Content-Security-Policy").Single());
        var html = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain("style=\"", html);
        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public async Task Inicio_de_sesion_correcto_lleva_al_panel_y_la_cookie_es_segura()
    {
        var c = _f.Cliente();
        var r = await FabricaWeb.IniciarSesionAsync(c, FabricaWeb.CorreoAdmin, _f.ContrasenaAdmin);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/", r.Headers.Location!.ToString());
        var cookie = r.Headers.GetValues("Set-Cookie").Single(v => v.StartsWith("Facturacion.Sesion="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);

        var panel = await c.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, panel.StatusCode);
        Assert.Contains("Panel principal", await panel.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Contrasena_incorrecta_muestra_mensaje_generico_y_queda_auditada()
    {
        var c = _f.Cliente();
        var r = await FabricaWeb.IniciarSesionAsync(c, FabricaWeb.CorreoFacturador, "Incorrecta-123!");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Correo o contraseña incorrectos.", await r.Content.ReadAsStringAsync());

        r = await FabricaWeb.IniciarSesionAsync(c, "no.existe@ejemplo.test", "Incorrecta-123!");
        Assert.Contains("Correo o contraseña incorrectos.", await r.Content.ReadAsStringAsync());

        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Auditoria.CountAsync(a => a.Accion == "Inicio de sesión fallido") >= 2);
        Assert.False(await db.Auditoria.AnyAsync(a => a.Detalle!.Contains("Incorrecta-123!")));
    }

    [Theory]
    [InlineData("/Respaldos")]
    [InlineData("/Usuarios")]
    [InlineData("/Auditoria")]
    [InlineData("/Anulaciones")]
    [InlineData("/Configuracion")]
    public async Task Facturador_no_accede_a_paginas_de_administracion(string url)
    {
        var c = _f.Cliente();
        await FabricaWeb.IniciarSesionAsync(c, FabricaWeb.CorreoFacturador, _f.ContrasenaFacturador);
        var r = await c.GetAsync(url);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/Cuenta/AccesoDenegado", r.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("/Documentos")]
    [InlineData("/Documentos/Emitir")]
    [InlineData("/Clientes")]
    [InlineData("/Productos")]
    public async Task Facturador_accede_a_operacion(string url)
    {
        var c = _f.Cliente();
        await FabricaWeb.IniciarSesionAsync(c, FabricaWeb.CorreoFacturador, _f.ContrasenaFacturador);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Facturador_no_puede_crear_respaldos_por_post_directo()
    {
        var c = _f.Cliente();
        await FabricaWeb.IniciarSesionAsync(c, FabricaWeb.CorreoFacturador, _f.ContrasenaFacturador);
        var token = await FabricaWeb.TokenAsync(c, "/Clientes/Crear");
        var r = await c.PostAsync("/Respaldos/Crear", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("AccesoDenegado", r.Headers.Location!.ToString());
        using var scope = _f.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Respaldos.AnyAsync());
    }

    [Fact]
    public async Task Post_sin_token_antifalsificacion_se_rechaza()
    {
        var c = _f.Cliente();
        await FabricaWeb.IniciarSesionAsync(c, FabricaWeb.CorreoAdmin, _f.ContrasenaAdmin);
        var r = await c.PostAsync("/Clientes/Crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Tipo"] = "ConsumidorFinal", ["Nombre"] = "PRUEBA CSRF"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        using var scope = _f.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Clientes.AnyAsync(x => x.Nombre == "PRUEBA CSRF"));
    }

    [Fact]
    public async Task Usuario_desactivado_no_inicia_sesion()
    {
        using (var scope = _f.Services.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<UsuarioAplicacion>>();
            var u = new UsuarioAplicacion { UserName = "inactivo@ejemplo.test", Email = "inactivo@ejemplo.test", NombreCompleto = "PRUEBA Inactivo", Activo = false };
            Assert.True((await um.CreateAsync(u, _f.ContrasenaFacturador)).Succeeded);
            await um.AddToRoleAsync(u, Facturacion.Application.Roles.Facturador);
        }
        var r = await FabricaWeb.IniciarSesionAsync(_f.Cliente(), "inactivo@ejemplo.test", _f.ContrasenaFacturador);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Correo o contraseña incorrectos.", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Redireccion_externa_tras_iniciar_sesion_se_ignora()
    {
        var c = _f.Cliente();
        var token = await FabricaWeb.TokenAsync(c, "/Cuenta/IniciarSesion");
        var r = await c.PostAsync("/Cuenta/IniciarSesion", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Correo"] = FabricaWeb.CorreoAdmin, ["Contrasena"] = _f.ContrasenaAdmin, ["VolverA"] = "https://sitio-externo.example/",
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal("/", r.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Ruta_inexistente_muestra_pagina_404_sin_detalles_internos()
    {
        var c = _f.Cliente();
        await FabricaWeb.IniciarSesionAsync(c, FabricaWeb.CorreoAdmin, _f.ContrasenaAdmin);
        var r = await c.GetAsync("/Documentos/Detalle/999999");
        var html = await r.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.DoesNotContain("Exception", html);
        Assert.DoesNotContain("at Facturacion", html);
    }
}

public class LimiteInicioSesionTests : IClassFixture<FabricaWebLimiteReal>
{
    private readonly FabricaWebLimiteReal _f;
    public LimiteInicioSesionTests(FabricaWebLimiteReal f) => _f = f;

    [Fact]
    public async Task Mas_de_diez_intentos_por_minuto_se_bloquean()
    {
        var c = _f.Cliente();
        var estados = new List<HttpStatusCode>();
        for (var i = 0; i < 11; i++)
            estados.Add((await FabricaWeb.IniciarSesionAsync(c, "nadie@ejemplo.test", "Incorrecta-123!")).StatusCode);
        Assert.All(estados.Take(10), s => Assert.Equal(HttpStatusCode.OK, s));
        Assert.Equal(HttpStatusCode.TooManyRequests, estados[10]);
    }
}

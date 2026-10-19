using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using Facturacion.Application;
using Facturacion.Application.Abstracciones;
using Facturacion.Infrastructure;
using Facturacion.Infrastructure.Identidad;
using Facturacion.Infrastructure.Persistencia;
using Facturacion.Web.Infraestructura;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.WebEncoders;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Registro de errores: consola y archivos diarios en la carpeta Logs (fuera de wwwroot).
builder.Host.UseSerilog((ctx, services, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(ctx.HostingEnvironment.ContentRootPath, "Logs", "facturacion-.log"),
        rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30));

builder.Services.AddFacturacionApplication();
builder.Services.AddFacturacionInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);

builder.Services
    .AddIdentity<UsuarioAplicacion, IdentityRole>(o =>
    {
        o.Password.RequiredLength = 10;
        o.Password.RequireDigit = true;
        o.Password.RequireLowercase = true;
        o.Password.RequireUppercase = true;
        o.Password.RequireNonAlphanumeric = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.Lockout.AllowedForNewUsers = true;
        o.User.RequireUniqueEmail = true;
        o.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()
    .AddErrorDescriber<DescriptorErroresIdentidad>();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "Facturacion.Sesion";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.LoginPath = "/Cuenta/IniciarSesion";
    o.LogoutPath = "/Cuenta/CerrarSesion";
    o.AccessDeniedPath = "/Cuenta/AccesoDenegado";
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
});
// Un usuario desactivado pierde la sesión en menos de un minuto.
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));
builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = "Facturacion.Antiforgery";
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Politicas.SoloAdministrador, p => p.RequireRole(Roles.Administrador))
    .AddPolicy(Politicas.Personal, p => p.RequireRole(Roles.Administrador, Roles.Facturador));

builder.Services.AddControllersWithViews(o =>
{
    // Todas las páginas exigen sesión iniciada salvo las marcadas con [AllowAnonymous],
    // y todos los formularios POST validan el token antifalsificación (CSRF).
    o.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    var m = o.ModelBindingMessageProvider;
    m.SetValueMustNotBeNullAccessor(_ => "Este campo es obligatorio.");
    m.SetAttemptedValueIsInvalidAccessor((v, _) => $"El valor '{v}' no es válido.");
    m.SetValueIsInvalidAccessor(v => $"El valor '{v}' no es válido.");
    m.SetValueMustBeANumberAccessor(_ => "Ingrese un número válido.");
    m.SetMissingBindRequiredValueAccessor(_ => "Este campo es obligatorio.");
    m.SetUnknownValueIsInvalidAccessor(_ => "El valor no es válido.");
    m.SetNonPropertyAttemptedValueIsInvalidAccessor(v => $"El valor '{v}' no es válido.");
    m.SetNonPropertyUnknownValueIsInvalidAccessor(() => "El valor no es válido.");
    m.SetNonPropertyValueMustBeANumberAccessor(() => "Ingrese un número válido.");
});

// Límite de intentos de inicio de sesión por dirección IP (Seguridad:IntentosInicioSesionPorMinuto, 10 por defecto).
var intentosPorMinuto = builder.Configuration.GetValue("Seguridad:IntentosInicioSesionPorMinuto", 10);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("inicio-sesion", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = intentosPorMinuto, Window = TimeSpan.FromMinutes(1) }));
});

// Razor escribe tildes y eñes tal cual (siguen codificados <, >, &, comillas): HTML legible y más liviano.
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioActual, UsuarioActualHttp>();
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 2 * 1024 * 1024);

var app = builder.Build();

// Comandos de administración: "dotnet run -- migrar" y "dotnet run -- crear-admin".
if (args.Length > 0 && await ComandosAdministracion.EjecutarAsync(app, args))
    return;

await InicializacionBaseDatos.EjecutarAsync(app);

var cultura = new CultureInfo("es-SV");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(cultura),
    SupportedCultures = new[] { cultura },
    SupportedUICultures = new[] { cultura }
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseExceptionHandler("/Error");
}
app.UseStatusCodePagesWithReExecute("/Error/{0}");
app.UseMiddleware<EncabezadosSeguridad>();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSerilogRequestLogging();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("default", "{controller=Inicio}/{action=Index}/{id?}");

app.Run();

/// <summary>Expuesto para las pruebas de integración (WebApplicationFactory).</summary>
public partial class Program;

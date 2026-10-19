using System.Text;
using Facturacion.Application;
using Facturacion.Domain;
using Facturacion.Domain.Entidades;
using Facturacion.Infrastructure.Identidad;
using Facturacion.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Web.Infraestructura;

/// <summary>
/// Comandos de consola para administrar la instalación sin exponer secretos en el código:
///   dotnet run -- migrar
///   dotnet run -- crear-admin --correo admin@ejemplo.com --nombre "Nombre Apellido"
/// La contraseña del administrador se lee de la variable de entorno FACTURACION_ADMIN_PASSWORD o se
/// solicita por consola sin mostrarla.
/// </summary>
public static class ComandosAdministracion
{
    public static async Task<bool> EjecutarAsync(WebApplication app, string[] args)
    {
        switch (args[0])
        {
            case "migrar":
                await MigrarAsync(app);
                return true;
            case "crear-admin":
                await CrearAdministradorAsync(app, args);
                return true;
            default:
                return false;
        }
    }

    private static async Task MigrarAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pendientes = (await db.Database.GetPendingMigrationsAsync()).ToList();
        Console.WriteLine(pendientes.Count == 0 ? "La base de datos ya está actualizada." : $"Aplicando {pendientes.Count} migración(es): {string.Join(", ", pendientes)}");
        await db.Database.MigrateAsync();
        Console.WriteLine("Migraciones aplicadas correctamente.");
    }

    private static string? Argumento(string[] args, string nombre)
    {
        var i = Array.IndexOf(args, nombre);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static async Task CrearAdministradorAsync(WebApplication app, string[] args)
    {
        var correo = Argumento(args, "--correo");
        var nombre = Argumento(args, "--nombre") ?? "Administrador";
        if (string.IsNullOrWhiteSpace(correo))
        {
            Console.Error.WriteLine("Uso: dotnet run -- crear-admin --correo admin@ejemplo.com [--nombre \"Nombre\"]");
            Environment.ExitCode = 1;
            return;
        }

        var contrasena = Environment.GetEnvironmentVariable("FACTURACION_ADMIN_PASSWORD") ?? LeerContrasenaOculta();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if ((await db.Database.GetPendingMigrationsAsync()).Any())
        {
            Console.Error.WriteLine("La base de datos tiene migraciones pendientes. Ejecute primero: dotnet run -- migrar");
            Environment.ExitCode = 1;
            return;
        }

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var rol in Roles.Todos)
            if (!await roles.RoleExistsAsync(rol)) await roles.CreateAsync(new IdentityRole(rol));

        var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<UsuarioAplicacion>>();
        if (await usuarios.FindByEmailAsync(correo) is not null)
        {
            Console.Error.WriteLine("Ya existe un usuario con ese correo. No se modificó nada.");
            Environment.ExitCode = 1;
            return;
        }

        var usuario = new UsuarioAplicacion { UserName = correo, Email = correo, EmailConfirmed = true, NombreCompleto = nombre, CreadoUtc = DateTime.UtcNow };
        var resultado = await usuarios.CreateAsync(usuario, contrasena);
        if (!resultado.Succeeded)
        {
            foreach (var e in resultado.Errors) Console.Error.WriteLine($"- {e.Description}");
            Environment.ExitCode = 1;
            return;
        }
        await usuarios.AddToRoleAsync(usuario, Roles.Administrador);
        db.Auditoria.Add(new RegistroAuditoria
        {
            FechaUtc = DateTime.UtcNow, Usuario = "consola", Accion = "Administrador inicial creado", Entidad = "Usuario",
            EntidadId = usuario.Id, Resultado = ResultadoOperacion.Exito, Detalle = correo
        });
        await db.SaveChangesAsync();
        Console.WriteLine($"Administrador {correo} creado.");
    }

    private static string LeerContrasenaOculta()
    {
        if (Console.IsInputRedirected) return Console.ReadLine() ?? "";
        Console.Write("Contraseña del administrador (mín. 10 caracteres, mayúscula, minúscula, dígito y símbolo): ");
        var sb = new StringBuilder();
        while (true)
        {
            var k = Console.ReadKey(intercept: true);
            if (k.Key == ConsoleKey.Enter) break;
            if (k.Key == ConsoleKey.Backspace) { if (sb.Length > 0) sb.Length--; }
            else if (!char.IsControl(k.KeyChar)) sb.Append(k.KeyChar);
        }
        Console.WriteLine();
        return sb.ToString();
    }
}

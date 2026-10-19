using Facturacion.Application;
using Facturacion.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Web.Infraestructura;

/// <summary>
/// Al iniciar: aplica migraciones solo si BaseDatos:MigrarAlIniciar = true (activado en Development) y crea
/// los roles si no existen. Nunca elimina ni recrea la base de datos.
/// </summary>
public static class InicializacionBaseDatos
{
    public static async Task EjecutarAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var config = app.Configuration;
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("InicializacionBaseDatos");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            if (config.GetValue("BaseDatos:MigrarAlIniciar", false))
            {
                if (db.Database.IsSqlServer()) await db.Database.MigrateAsync();
                else await db.Database.EnsureCreatedAsync(); // SQLite: solo pruebas automatizadas.
            }

            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var rol in Roles.Todos)
            {
                if (!await roles.RoleExistsAsync(rol)) await roles.CreateAsync(new IdentityRole(rol));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo inicializar la base de datos. Revise la cadena de conexión y ejecute 'dotnet run -- migrar'.");
            throw;
        }

        if (config["Facturacion:Modo"]?.Equals("Oficial", StringComparison.OrdinalIgnoreCase) == true)
            logger.LogWarning("Facturacion:Modo = Oficial, pero la integración oficial no está implementada: la emisión será rechazada.");
    }
}

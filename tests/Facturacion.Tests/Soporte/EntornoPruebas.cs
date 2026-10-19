using Facturacion.Application;
using Facturacion.Application.Abstracciones;
using Facturacion.Application.Servicios;
using Facturacion.Domain;
using Facturacion.Domain.Entidades;
using Facturacion.Infrastructure;
using Facturacion.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Facturacion.Tests.Soporte;

/// <summary>
/// Entorno aislado para pruebas de integración: una base SQLite NUEVA en una carpeta temporal propia de la
/// prueba (nunca la base de SQL Server de la aplicación), carpeta de documentos temporal, reloj controlable,
/// correo falso y datos sintéticos claramente identificados. Al terminar se borra solo esa carpeta temporal.
/// </summary>
public sealed class EntornoPruebas : IAsyncDisposable
{
    public string Carpeta { get; }
    public ServiceProvider Servicios { get; }
    public RelojPrueba Reloj { get; } = new();
    public UsuarioPrueba Usuario { get; } = new();
    public EnviadorCorreoFalso Correo { get; } = new();
    public ColaCorreoPrueba Cola { get; } = new();

    private EntornoPruebas(Dictionary<string, string?>? extra)
    {
        Carpeta = Path.Combine(Path.GetTempPath(), "facturacion-pruebas", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Carpeta);
        var valores = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Facturacion"] = $"Data Source={Path.Combine(Carpeta, "prueba.db")};Pooling=False",
            ["BaseDatos:Proveedor"] = "Sqlite",
            ["Correo:DeshabilitarTrabajador"] = "true",
            ["Almacenamiento:RutaDocumentos"] = Path.Combine(Carpeta, "Documentos"),
            ["Respaldos:Directorio"] = Path.Combine(Carpeta, "Respaldos"),
            ["Facturacion:Modo"] = "Academico",
        };
        if (extra is not null) foreach (var (k, v) in extra) valores[k] = v;
        var config = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IConfiguration>(config);
        services.AddFacturacionApplication();
        services.AddFacturacionInfrastructure(config, Carpeta);
        services.AddSingleton<IReloj>(Reloj);
        services.AddSingleton<IUsuarioActual>(Usuario);
        services.AddSingleton<IEnviadorCorreo>(Correo);
        services.AddSingleton<IColaCorreo>(Cola);
        Servicios = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public static async Task<EntornoPruebas> CrearAsync(Dictionary<string, string?>? extra = null, bool conEmisor = true)
    {
        var e = new EntornoPruebas(extra);
        await using var scope = e.Servicios.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        if (conEmisor)
        {
            db.ConfiguracionEmisor.Add(DatosSinteticos.Emisor());
            await db.SaveChangesAsync();
        }
        return e;
    }

    public AsyncServiceScope Alcance() => Servicios.CreateAsyncScope();

    /// <summary>Ejecuta una acción con un alcance (y DbContext) nuevo, como una petición web independiente.</summary>
    public async Task<T> ConAsync<T>(Func<IServiceProvider, Task<T>> accion)
    {
        await using var scope = Servicios.CreateAsyncScope();
        return await accion(scope.ServiceProvider);
    }

    public async Task ConAsync(Func<IServiceProvider, Task> accion)
    {
        await using var scope = Servicios.CreateAsyncScope();
        await accion(scope.ServiceProvider);
    }

    public Task<T> DbAsync<T>(Func<AppDbContext, Task<T>> consulta) =>
        ConAsync(sp => consulta(sp.GetRequiredService<AppDbContext>()));

    public async Task<Cliente> CrearClienteAsync(Cliente c) =>
        await ConAsync(sp => sp.GetRequiredService<ClienteService>().CrearAsync(c));

    public async Task<Producto> CrearProductoAsync(Producto p) =>
        await ConAsync(sp => sp.GetRequiredService<ProductoService>().CrearAsync(p));

    public Task<ResultadoEmision> EmitirAsync(SolicitudEmision s, Guid? clave = null) =>
        ConAsync(sp => sp.GetRequiredService<EmisionService>().EmitirAsync(s, clave ?? Guid.NewGuid()));

    /// <summary>Procesa con el enviador falso todos los envíos que la aplicación encoló.</summary>
    public async Task<List<EstadoEnvio>> ProcesarCorreosAsync()
    {
        var estados = new List<EstadoEnvio>();
        while (Cola.Encolados.TryDequeue(out var id))
            estados.Add(await ConAsync(sp => sp.GetRequiredService<CorreoService>().ProcesarAsync(id)));
        return estados;
    }

    public async ValueTask DisposeAsync()
    {
        await Servicios.DisposeAsync();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(Carpeta, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

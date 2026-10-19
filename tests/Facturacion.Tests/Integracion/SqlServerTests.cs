using Facturacion.Application;
using Facturacion.Application.Abstracciones;
using Facturacion.Application.Servicios;
using Facturacion.Domain;
using Facturacion.Infrastructure;
using Facturacion.Infrastructure.Persistencia;
using Facturacion.Tests.Soporte;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Facturacion.Tests.Integracion;

/// <summary>
/// Pruebas opcionales contra SQL Server real. Solo se ejecutan si existe la variable de entorno
/// FACTURACION_PRUEBAS_SQLSERVER con una cadena de conexión a un servidor de PRUEBAS. Cada ejecución crea una
/// base de datos nueva con nombre único (FacturacionPruebas_xxxx), aplica las migraciones y al final elimina
/// únicamente esa base creada por la prueba. Nunca se usa ni modifica la base de datos de la aplicación.
/// </summary>
public sealed class SqlServerTests : IAsyncLifetime
{
    private readonly string? _servidor = Environment.GetEnvironmentVariable("FACTURACION_PRUEBAS_SQLSERVER");
    private readonly string _baseDatos = $"FacturacionPruebas_{Guid.NewGuid():N}"[..40];
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "facturacion-pruebas-sql", Guid.NewGuid().ToString("N"));
    private ServiceProvider? _sp;
    private readonly RelojPrueba _reloj = new();
    private readonly EnviadorCorreoFalso _correo = new();
    private readonly ColaCorreoPrueba _cola = new();

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_servidor)) return;
        var cadena = new SqlConnectionStringBuilder(_servidor) { InitialCatalog = _baseDatos }.ConnectionString;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Facturacion"] = cadena,
            ["BaseDatos:Proveedor"] = "SqlServer",
            ["Correo:DeshabilitarTrabajador"] = "true",
            ["Almacenamiento:RutaDocumentos"] = Path.Combine(_carpeta, "Documentos"),
        }).Build();
        var s = new ServiceCollection();
        s.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        s.AddFacturacionApplication();
        s.AddFacturacionInfrastructure(config, _carpeta);
        s.AddSingleton<IReloj>(_reloj);
        s.AddSingleton<IUsuarioActual>(new UsuarioPrueba());
        s.AddSingleton<IEnviadorCorreo>(_correo);
        s.AddSingleton<IColaCorreo>(_cola);
        _sp = s.BuildServiceProvider();

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        db.ConfiguracionEmisor.Add(DatosSinteticos.Emisor());
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (_sp is null) return;
        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Solo se elimina la base creada por esta prueba (nombre único generado arriba).
            if (db.Database.GetDbConnection().Database == _baseDatos) await db.Database.EnsureDeletedAsync();
        }
        await _sp.DisposeAsync();
        try { Directory.Delete(_carpeta, true); } catch (IOException) { }
    }

    private async Task<T> Con<T>(Func<IServiceProvider, Task<T>> f)
    {
        using var scope = _sp!.CreateScope();
        return await f(scope.ServiceProvider);
    }

    [SkippableFact]
    public async Task Emision_concurrente_en_sql_server_mantiene_correlativos_e_inventario_consistentes()
    {
        Skip.If(_sp is null, "Defina FACTURACION_PRUEBAS_SQLSERVER para ejecutar las pruebas con SQL Server.");
        var cliente = await Con(sp => sp.GetRequiredService<ClienteService>().CrearAsync(DatosSinteticos.ConsumidorFinal()));
        var producto = await Con(sp => sp.GetRequiredService<ProductoService>().CrearAsync(DatosSinteticos.Producto(inventario: true, existencias: 5)));

        // 8 emisiones simultáneas de 1 unidad con solo 5 en existencia: exactamente 5 deben emitirse.
        var tareas = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            try
            {
                await Con(sp => sp.GetRequiredService<EmisionService>().EmitirAsync(
                    DatosSinteticos.Venta(TipoDte.FacturaConsumidorFinal, cliente.Id, (producto.Id, 1, 0)), Guid.NewGuid()));
                return true;
            }
            catch (Facturacion.Domain.Reglas.ReglaNegocioException) { return false; }
        })).ToList();
        var resultados = await Task.WhenAll(tareas);

        Assert.Equal(5, resultados.Count(x => x));
        var numeros = await Con(sp => sp.GetRequiredService<AppDbContext>().Documentos.Select(d => d.NumeroControl).ToListAsync());
        Assert.Equal(5, numeros.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 5).Select(i => $"DTE-01-M001P001-{i:D15}"), numeros.OrderBy(n => n));
        Assert.Equal(0m, await Con(sp => sp.GetRequiredService<AppDbContext>().Productos.Where(p => p.Id == producto.Id).Select(p => p.Existencias).SingleAsync()));
    }

    [SkippableFact]
    public async Task Desencadenador_impide_modificar_la_auditoria_aun_con_sql_directo()
    {
        Skip.If(_sp is null, "Defina FACTURACION_PRUEBAS_SQLSERVER para ejecutar las pruebas con SQL Server.");
        await Con(sp => sp.GetRequiredService<ClienteService>().CrearAsync(DatosSinteticos.ConsumidorFinal()));
        var ex = await Assert.ThrowsAsync<SqlException>(() => Con(sp =>
            sp.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync("UPDATE Auditoria SET Detalle = 'alterado'")));
        Assert.Contains("solo lectura", ex.Message);
        await Assert.ThrowsAsync<SqlException>(() => Con(sp =>
            sp.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync("DELETE FROM Auditoria")));
    }

    [SkippableFact]
    public async Task Respaldo_sql_server_se_crea_y_verifica()
    {
        Skip.If(_sp is null, "Defina FACTURACION_PRUEBAS_SQLSERVER para ejecutar las pruebas con SQL Server.");
        var r = await Con(sp => sp.GetRequiredService<RespaldoService>().CrearAsync());
        Assert.Equal(EstadoRespaldo.Exitoso, r.Estado);
        Assert.True(r.Verificado);
        Assert.True(r.TamanoBytes > 0);
    }
}

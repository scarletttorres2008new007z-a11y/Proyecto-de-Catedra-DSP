using Facturacion.Application.Abstracciones;
using Facturacion.Application.Configuracion;
using Facturacion.Domain;
using Facturacion.Infrastructure.Archivos;
using Facturacion.Infrastructure.Correo;
using Facturacion.Infrastructure.Hacienda;
using Facturacion.Infrastructure.Pdf;
using Facturacion.Infrastructure.Persistencia;
using Facturacion.Infrastructure.Respaldos;
using Facturacion.Infrastructure.Servicios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Facturacion.Infrastructure;

public static class DependencyInjection
{
    public const string NombreCadenaConexion = "Facturacion";

    /// <summary>
    /// Registra persistencia (SQL Server), archivos, PDF, correo, transmisor y respaldos.
    /// El proveedor "Sqlite" existe exclusivamente para pruebas automatizadas aisladas (BaseDatos:Proveedor).
    /// </summary>
    public static IServiceCollection AddFacturacionInfrastructure(this IServiceCollection services, IConfiguration config, string contentRoot)
    {
        var cadena = config.GetConnectionString(NombreCadenaConexion)
                     ?? throw new InvalidOperationException($"Falta la cadena de conexión 'ConnectionStrings:{NombreCadenaConexion}'.");
        var proveedor = config["BaseDatos:Proveedor"] ?? "SqlServer";

        services.AddDbContext<AppDbContext>(o =>
        {
            if (proveedor.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
                o.UseSqlite(cadena);
            else
                o.UseSqlServer(cadena, sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<OpcionesFacturacion>(config.GetSection(OpcionesFacturacion.Seccion));
        services.Configure<OpcionesAnulacion>(config.GetSection(OpcionesAnulacion.Seccion));
        services.Configure<OpcionesSmtp>(config.GetSection(OpcionesSmtp.Seccion));

        var almacenamiento = config.GetSection(OpcionesAlmacenamiento.Seccion).Get<OpcionesAlmacenamiento>() ?? new OpcionesAlmacenamiento();
        var rutaDocumentos = Path.IsPathRooted(almacenamiento.RutaDocumentos)
            ? almacenamiento.RutaDocumentos
            : Path.Combine(contentRoot, almacenamiento.RutaDocumentos);
        services.AddSingleton<IAlmacenArchivos>(_ => new AlmacenArchivosLocal(rutaDocumentos));

        services.AddSingleton<IReloj, RelojElSalvador>();
        services.AddSingleton<IGeneradorPdf, GeneradorPdfQuest>();
        services.AddSingleton<IEnviadorCorreo, EnviadorCorreoMailKit>();
        services.AddScoped<IAuditoria, AuditoriaEf>();

        var modo = config.GetSection(OpcionesFacturacion.Seccion).Get<OpcionesFacturacion>()?.Modo ?? ModoOperacion.Academico;
        if (modo == ModoOperacion.Oficial) services.AddSingleton<ITransmisorHacienda, TransmisorOficialNoImplementado>();
        else services.AddSingleton<ITransmisorHacienda, TransmisorAcademico>();

        services.AddSingleton<ColaCorreo>();
        services.AddSingleton<IColaCorreo>(sp => sp.GetRequiredService<ColaCorreo>());
        if (!config.GetValue("Correo:DeshabilitarTrabajador", false))
            services.AddHostedService<TrabajadorCorreo>();

        var respaldos = config.GetSection(OpcionesRespaldo.Seccion).Get<OpcionesRespaldo>() ?? new OpcionesRespaldo();
        if (proveedor.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var dir = string.IsNullOrWhiteSpace(respaldos.Directorio) ? Path.Combine(contentRoot, "App_Data", "Respaldos") : respaldos.Directorio!;
            services.AddScoped<IProveedorRespaldo>(_ => new ProveedorRespaldoSqlite(cadena, dir));
        }
        else
        {
            services.AddScoped<IProveedorRespaldo>(sp =>
                new ProveedorRespaldoSqlServer(cadena, respaldos, sp.GetRequiredService<ILogger<ProveedorRespaldoSqlServer>>()));
        }

        return services;
    }
}

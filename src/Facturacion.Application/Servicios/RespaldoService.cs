using Facturacion.Application.Abstracciones;
using Facturacion.Domain;
using Facturacion.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Facturacion.Application.Servicios;

/// <summary>
/// Copias de seguridad "Backup (fecha)". El resultado que se registra es el que devuelve el motor de base de
/// datos: nunca se marca como exitosa una copia cuyo proceso falló. La restauración no se automatiza.
/// </summary>
public sealed class RespaldoService(
    IAppDbContext db,
    IReloj reloj,
    IUsuarioActual usuario,
    IAuditoria auditoria,
    IProveedorRespaldo proveedor,
    ILogger<RespaldoService> logger)
{
    public string Motor => proveedor.Motor;
    public string Directorio => proveedor.DirectorioDestino;

    public async Task<Respaldo> CrearAsync(CancellationToken ct = default)
    {
        var inicio = reloj.UtcNow;
        var local = reloj.ALocal(inicio);
        var nombre = $"Backup_{local:yyyy-MM-dd_HHmmss}{(proveedor.Motor == "SQL Server" ? ".bak" : ".db")}";

        ResultadoRespaldoMotor resultado;
        try
        {
            resultado = await proveedor.CrearAsync(nombre, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Error al crear la copia de seguridad {Nombre}", nombre);
            resultado = new ResultadoRespaldoMotor(false, "", null, false,
                "El motor de base de datos rechazó la copia de seguridad. Revise el registro del servidor y los permisos de la carpeta de respaldos.");
        }

        var respaldo = new Respaldo
        {
            IniciadoUtc = inicio,
            FinalizadoUtc = reloj.UtcNow,
            Usuario = usuario.Nombre,
            NombreArchivo = nombre,
            RutaArchivo = resultado.RutaArchivo,
            TamanoBytes = resultado.TamanoBytes,
            Estado = resultado.Exitoso ? EstadoRespaldo.Exitoso : EstadoRespaldo.Fallido,
            Verificado = resultado.Verificado,
            Mensaje = resultado.Mensaje
        };
        db.Respaldos.Add(respaldo);
        auditoria.Agregar(resultado.Exitoso ? "Copia de seguridad creada" : "Copia de seguridad fallida", nameof(Respaldo), nombre,
            resultado.Exitoso ? ResultadoOperacion.Exito : ResultadoOperacion.Error, resultado.Mensaje);
        await db.SaveChangesAsync(ct);
        return respaldo;
    }

    public async Task<Pagina<Respaldo>> ListarAsync(int pagina, CancellationToken ct = default)
    {
        pagina = PaginacionExtensions.Normalizar(pagina);
        var q = db.Respaldos.AsNoTracking();
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(r => r.IniciadoUtc)
            .Skip((pagina - 1) * PaginacionExtensions.TamanoPorDefecto).Take(PaginacionExtensions.TamanoPorDefecto).ToListAsync(ct);
        return new Pagina<Respaldo>(items, pagina, PaginacionExtensions.TamanoPorDefecto, total);
    }
}

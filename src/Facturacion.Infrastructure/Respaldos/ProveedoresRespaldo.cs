using Facturacion.Application.Abstracciones;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Facturacion.Infrastructure.Respaldos;

/// <summary>Sección "Respaldos" de la configuración.</summary>
public sealed class OpcionesRespaldo
{
    public const string Seccion = "Respaldos";

    /// <summary>
    /// Carpeta de destino EN EL SERVIDOR DE SQL SERVER (el servicio de SQL Server debe poder escribir en ella).
    /// Si se deja vacía se usa la carpeta de respaldos predeterminada de la instancia.
    /// </summary>
    public string? Directorio { get; set; }

    public int TiempoEsperaSegundos { get; set; } = 600;
}

/// <summary>
/// Copia completa con BACKUP DATABASE ... WITH CHECKSUM y verificación con RESTORE VERIFYONLY. El tamaño se
/// obtiene del historial msdb.dbo.backupset. No restaura nada.
/// </summary>
public sealed class ProveedorRespaldoSqlServer(string cadenaConexion, OpcionesRespaldo opciones, ILogger<ProveedorRespaldoSqlServer> logger) : IProveedorRespaldo
{
    public string Motor => "SQL Server";

    public string DirectorioDestino => string.IsNullOrWhiteSpace(opciones.Directorio)
        ? "(carpeta de respaldos predeterminada de la instancia de SQL Server)"
        : opciones.Directorio!;

    public async Task<ResultadoRespaldoMotor> CrearAsync(string nombreArchivo, CancellationToken ct = default)
    {
        var builder = new SqlConnectionStringBuilder(cadenaConexion);
        var baseDatos = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(baseDatos))
            return new ResultadoRespaldoMotor(false, "", null, false, "La cadena de conexión no indica la base de datos.");

        await using var cn = new SqlConnection(cadenaConexion);
        await cn.OpenAsync(ct);

        var directorio = opciones.Directorio;
        if (string.IsNullOrWhiteSpace(directorio))
        {
            await using var cmdDir = new SqlCommand("SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(400))", cn);
            directorio = (await cmdDir.ExecuteScalarAsync(ct)) as string;
            if (string.IsNullOrWhiteSpace(directorio))
                return new ResultadoRespaldoMotor(false, "", null, false, "No se pudo determinar la carpeta de respaldos de SQL Server. Configure Respaldos:Directorio.");
        }
        // El separador depende del sistema operativo del servidor SQL (Windows o Linux).
        var separador = directorio.Contains('\\') ? '\\' : '/';
        var ruta = directorio.TrimEnd('\\', '/') + separador + nombreArchivo;

        // El nombre de la base de datos no se puede parametrizar en BACKUP: se delimita con corchetes escapados.
        var nombreSeguro = "[" + baseDatos.Replace("]", "]]") + "]";
        await using (var backup = new SqlCommand(
                         $"BACKUP DATABASE {nombreSeguro} TO DISK = @ruta WITH INIT, CHECKSUM, NAME = @nombre", cn))
        {
            backup.CommandTimeout = opciones.TiempoEsperaSegundos;
            backup.Parameters.AddWithValue("@ruta", ruta);
            backup.Parameters.AddWithValue("@nombre", nombreArchivo);
            try
            {
                await backup.ExecuteNonQueryAsync(ct);
            }
            catch (SqlException ex)
            {
                logger.LogError(ex, "BACKUP DATABASE falló para {Ruta}", ruta);
                return new ResultadoRespaldoMotor(false, ruta, null, false,
                    $"SQL Server no pudo crear la copia (error {ex.Number}). Verifique que la carpeta exista y que el servicio de SQL Server tenga permiso de escritura.");
            }
        }

        bool verificado;
        string mensajeVerificacion;
        await using (var verify = new SqlCommand("RESTORE VERIFYONLY FROM DISK = @ruta WITH CHECKSUM", cn))
        {
            verify.CommandTimeout = opciones.TiempoEsperaSegundos;
            verify.Parameters.AddWithValue("@ruta", ruta);
            try
            {
                await verify.ExecuteNonQueryAsync(ct);
                verificado = true;
                mensajeVerificacion = "Verificada con RESTORE VERIFYONLY (CHECKSUM).";
            }
            catch (SqlException ex)
            {
                logger.LogError(ex, "RESTORE VERIFYONLY falló para {Ruta}", ruta);
                verificado = false;
                mensajeVerificacion = $"La verificación RESTORE VERIFYONLY falló (error {ex.Number}).";
            }
        }

        long? tamano = null;
        await using (var size = new SqlCommand(
                         @"SELECT TOP 1 CAST(bs.backup_size AS bigint)
                           FROM msdb.dbo.backupset bs
                           JOIN msdb.dbo.backupmediafamily mf ON bs.media_set_id = mf.media_set_id
                           WHERE mf.physical_device_name = @ruta
                           ORDER BY bs.backup_finish_date DESC", cn))
        {
            size.Parameters.AddWithValue("@ruta", ruta);
            if (await size.ExecuteScalarAsync(ct) is long t) tamano = t;
        }

        // Si la carpeta es accesible desde la aplicación (SQL Server local), se comprueba también el archivo.
        try
        {
            if (File.Exists(ruta)) tamano = new FileInfo(ruta).Length;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "No se pudo inspeccionar el archivo de respaldo localmente");
        }

        var exitoso = verificado && tamano is > 0;
        return new ResultadoRespaldoMotor(exitoso, ruta, tamano, verificado,
            exitoso ? $"Copia completa creada. {mensajeVerificacion}" : $"La copia no pudo confirmarse. {mensajeVerificacion}");
    }
}

/// <summary>Respaldo para SQLite (solo pruebas automatizadas): VACUUM INTO y PRAGMA integrity_check.</summary>
public sealed class ProveedorRespaldoSqlite(string cadenaConexion, string directorio) : IProveedorRespaldo
{
    public string Motor => "SQLite";
    public string DirectorioDestino => directorio;

    public async Task<ResultadoRespaldoMotor> CrearAsync(string nombreArchivo, CancellationToken ct = default)
    {
        Directory.CreateDirectory(directorio);
        var ruta = Path.Combine(directorio, nombreArchivo);
        if (File.Exists(ruta)) return new ResultadoRespaldoMotor(false, ruta, null, false, "Ya existe un respaldo con ese nombre.");

        await using (var cn = new SqliteConnection(cadenaConexion))
        {
            await cn.OpenAsync(ct);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "VACUUM INTO $ruta";
            cmd.Parameters.AddWithValue("$ruta", ruta);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        var info = new FileInfo(ruta);
        if (!info.Exists || info.Length == 0) return new ResultadoRespaldoMotor(false, ruta, null, false, "El archivo de respaldo no se creó.");

        string? integridad;
        await using (var cn = new SqliteConnection($"Data Source={ruta};Mode=ReadOnly;Pooling=False"))
        {
            await cn.OpenAsync(ct);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check";
            integridad = (await cmd.ExecuteScalarAsync(ct))?.ToString();
        }
        var ok = integridad == "ok";
        return new ResultadoRespaldoMotor(ok, ruta, info.Length, ok, ok ? "Copia creada y verificada (integrity_check)." : "La verificación de integridad falló.");
    }
}

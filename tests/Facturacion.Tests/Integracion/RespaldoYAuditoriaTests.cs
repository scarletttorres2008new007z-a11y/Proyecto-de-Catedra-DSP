using Facturacion.Application.Abstracciones;
using Facturacion.Application.Servicios;
using Facturacion.Domain;
using Facturacion.Infrastructure.Persistencia;
using Facturacion.Tests.Soporte;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Facturacion.Tests.Integracion;

public class RespaldoYAuditoriaTests
{
    [Fact]
    public async Task Escenario_F_respaldo_crea_archivo_verificado_y_registra_el_resultado_real()
    {
        await using var env = await EntornoPruebas.CrearAsync();
        await env.CrearClienteAsync(DatosSinteticos.ConsumidorFinal());

        var r = await env.ConAsync(sp => sp.GetRequiredService<RespaldoService>().CrearAsync());

        Assert.Equal(EstadoRespaldo.Exitoso, r.Estado);
        Assert.True(r.Verificado);
        Assert.Equal("Backup_2026-03-10_100000.db", r.NombreArchivo);
        Assert.True(File.Exists(r.RutaArchivo));
        Assert.Equal(new FileInfo(r.RutaArchivo).Length, r.TamanoBytes);

        // La copia es una base de datos legible con los mismos datos (se abre en modo de solo lectura).
        await using var con = new SqliteConnection($"Data Source={r.RutaArchivo};Mode=ReadOnly;Pooling=False");
        await con.OpenAsync();
        await using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Clientes";
        Assert.Equal(1L, (long)(await cmd.ExecuteScalarAsync())!);
        Assert.Contains("Copia de seguridad creada", await env.DbAsync(db => db.Auditoria.Select(a => a.Accion).ToListAsync()));
    }

    [Fact]
    public async Task Respaldo_fallido_se_registra_como_fallido()
    {
        await using var env = await EntornoPruebas.CrearAsync();
        // El destino de respaldos es un archivo, no una carpeta: el motor no puede escribir.
        await File.WriteAllTextAsync(Path.Combine(env.Carpeta, "Respaldos"), "bloqueo");

        var r = await env.ConAsync(sp => sp.GetRequiredService<RespaldoService>().CrearAsync());

        Assert.Equal(EstadoRespaldo.Fallido, r.Estado);
        Assert.False(r.Verificado);
        Assert.Contains("Copia de seguridad fallida", await env.DbAsync(db => db.Auditoria.Select(a => a.Accion).ToListAsync()));
    }

    [Fact]
    public async Task Auditoria_no_permite_modificar_ni_eliminar_registros()
    {
        await using var env = await EntornoPruebas.CrearAsync();
        await env.CrearClienteAsync(DatosSinteticos.ConsumidorFinal());
        await env.ConAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var registro = await db.Auditoria.FirstAsync();
            registro.Detalle = "alterado";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            db.Auditoria.Remove(await db.Auditoria.FirstAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        });
        Assert.DoesNotContain("alterado", await env.DbAsync(db => db.Auditoria.Select(a => a.Detalle ?? "").ToListAsync()));
    }

    [Fact]
    public async Task Escenario_E_consulta_de_auditoria_filtra_por_accion_y_usuario()
    {
        await using var env = await EntornoPruebas.CrearAsync();
        await env.CrearClienteAsync(DatosSinteticos.ConsumidorFinal());
        env.Usuario.Nombre = "otro.usuario@ejemplo.test";
        await env.CrearProductoAsync(DatosSinteticos.Producto());

        var pagina = await env.ConAsync(sp => sp.GetRequiredService<AuditoriaConsultaService>()
            .ListarAsync(new FiltroAuditoria(null, null, null, null, null, "otro.usuario", 1)));
        Assert.NotEmpty(pagina.Elementos);
        Assert.All(pagina.Elementos, a => Assert.Equal("otro.usuario@ejemplo.test", a.Usuario));
    }

    [Fact]
    public async Task Edicion_concurrente_de_un_cliente_se_detecta()
    {
        await using var env = await EntornoPruebas.CrearAsync();
        var c = await env.CrearClienteAsync(DatosSinteticos.ConsumidorFinal());
        var copiaA = await env.ConAsync(sp => sp.GetRequiredService<ClienteService>().ObtenerAsync(c.Id));
        var copiaB = await env.ConAsync(sp => sp.GetRequiredService<ClienteService>().ObtenerAsync(c.Id));

        copiaA!.Nombre = "PRUEBA Nombre A";
        await env.ConAsync(sp => sp.GetRequiredService<ClienteService>().ActualizarAsync(copiaA));
        copiaB!.Nombre = "PRUEBA Nombre B";
        await Assert.ThrowsAnyAsync<Exception>(() => env.ConAsync(sp => sp.GetRequiredService<ClienteService>().ActualizarAsync(copiaB)));

        Assert.Equal("PRUEBA Nombre A", (await env.ConAsync(sp => sp.GetRequiredService<ClienteService>().ObtenerAsync(c.Id)))!.Nombre);
    }

    [Fact]
    public async Task Cliente_con_documento_duplicado_se_rechaza()
    {
        await using var env = await EntornoPruebas.CrearAsync();
        await env.CrearClienteAsync(DatosSinteticos.Contribuyente());
        await Assert.ThrowsAsync<Facturacion.Domain.Reglas.ReglaNegocioException>(() => env.CrearClienteAsync(DatosSinteticos.Contribuyente()));
    }
}

using Facturacion.Application.Abstracciones;
using Facturacion.Application.Servicios;
using Facturacion.Domain;
using Facturacion.Domain.Reglas;
using Facturacion.Tests.Soporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Facturacion.Tests.Integracion;

/// <summary>Envío y reenvío con un enviador falso: ninguna prueba envía correos reales.</summary>
public class CorreoTests
{
    private static async Task<(EntornoPruebas Env, int DocumentoId)> EmitirFacturaAsync(string? correoCliente = "consumidor@ejemplo.test")
    {
        var env = await EntornoPruebas.CrearAsync();
        var cf = await env.CrearClienteAsync(DatosSinteticos.ConsumidorFinal(correoCliente));
        var p = await env.CrearProductoAsync(DatosSinteticos.Producto());
        var r = await env.EmitirAsync(DatosSinteticos.Venta(TipoDte.FacturaConsumidorFinal, cf.Id, (p.Id, 1, 0)));
        return (env, r.DocumentoId);
    }

    [Fact]
    public async Task Envio_automatico_adjunta_json_y_pdf_con_el_codigo_de_generacion()
    {
        var (env, docId) = await EmitirFacturaAsync();
        await using var _ = env;

        Assert.Equal(new[] { EstadoEnvio.EnviadoServidorSmtp }, await env.ProcesarCorreosAsync());

        var doc = await env.DbAsync(db => db.Documentos.Include(d => d.Envios).SingleAsync(d => d.Id == docId));
        var codigo = doc.CodigoGeneracion.ToString().ToUpperInvariant();
        var mensaje = Assert.Single(env.Correo.Enviados);
        Assert.Equal("consumidor@ejemplo.test", mensaje.Destinatario);
        Assert.Contains(doc.NumeroControl, mensaje.Asunto);
        Assert.Equal(new[] { $"{codigo}.json", $"{codigo}.pdf" }, mensaje.Adjuntos.Select(a => a.NombreArchivo));
        Assert.Equal(new[] { "application/json", "application/pdf" }, mensaje.Adjuntos.Select(a => a.TipoContenido));
        Assert.Contains("modo de demostración académica", mensaje.CuerpoTexto);
        Assert.DoesNotContain("<script", mensaje.CuerpoHtml);

        var envio = Assert.Single(doc.Envios);
        Assert.Equal(EstadoEnvio.EnviadoServidorSmtp, envio.Estado);
        Assert.NotNull(envio.ProcesadoUtc);
        Assert.Null(envio.MensajeError);
    }

    [Theory]
    [InlineData(TipoFalloCorreo.Autenticacion, "El servidor SMTP rechazó las credenciales.")]
    [InlineData(TipoFalloCorreo.Conexion, "No se pudo conectar con el servidor SMTP.")]
    [InlineData(TipoFalloCorreo.TiempoAgotado, "Se agotó el tiempo de espera del servidor SMTP.")]
    public async Task Fallo_smtp_queda_registrado_con_mensaje_seguro_y_el_documento_sigue_emitido(TipoFalloCorreo tipo, string mensaje)
    {
        var (env, docId) = await EmitirFacturaAsync();
        await using var _ = env;
        env.Correo.FalloSiguiente = new CorreoException(tipo, mensaje, new Exception("detalle interno con Password=secreto"));

        Assert.Equal(new[] { EstadoEnvio.Fallido }, await env.ProcesarCorreosAsync());

        var doc = await env.DbAsync(db => db.Documentos.Include(d => d.Envios).SingleAsync(d => d.Id == docId));
        Assert.Equal(EstadoDocumento.Emitido, doc.Estado);
        var envio = Assert.Single(doc.Envios);
        Assert.Equal(mensaje, envio.MensajeError);
        Assert.DoesNotContain("secreto", envio.MensajeError);
        var auditoria = await env.DbAsync(db => db.Auditoria.Where(a => a.Accion == "Envío de correo fallido").ToListAsync());
        Assert.DoesNotContain(auditoria, a => a.Detalle!.Contains("secreto"));
    }

    [Fact]
    public async Task Cliente_sin_correo_registra_fallo_sin_intentar_enviar()
    {
        var (env, docId) = await EmitirFacturaAsync(correoCliente: null);
        await using var _ = env;
        Assert.Equal(new[] { EstadoEnvio.Fallido }, await env.ProcesarCorreosAsync());
        Assert.Empty(env.Correo.Enviados);
        var envio = await env.DbAsync(db => db.EnviosCorreo.SingleAsync(e => e.DocumentoId == docId));
        Assert.Contains("no tiene un correo", envio.MensajeError);
    }

    [Fact]
    public async Task Reenvio_crea_un_nuevo_intento_sin_emitir_otro_documento()
    {
        var (env, docId) = await EmitirFacturaAsync();
        await using var _ = env;
        env.Correo.FalloSiguiente = new CorreoException(TipoFalloCorreo.Conexion, "No se pudo conectar con el servidor SMTP.");
        await env.ProcesarCorreosAsync();

        var antes = await env.DbAsync(db => db.Documentos.Include(d => d.Archivos).SingleAsync(d => d.Id == docId));
        var envio = await env.ConAsync(sp => sp.GetRequiredService<CorreoService>().SolicitarEnvioAsync(docId, MotivoEnvio.Reenvio, "otro.destino@ejemplo.test"));
        Assert.Equal(2, envio.NumeroIntento);
        Assert.Equal(new[] { EstadoEnvio.EnviadoServidorSmtp }, await env.ProcesarCorreosAsync());

        var despues = await env.DbAsync(db => db.Documentos.Include(d => d.Archivos).Include(d => d.Envios).SingleAsync(d => d.Id == docId));
        Assert.Equal(1, await env.DbAsync(db => db.Documentos.CountAsync()));
        Assert.Equal(antes.Archivos.Select(a => a.Sha256).OrderBy(x => x), despues.Archivos.Select(a => a.Sha256).OrderBy(x => x));
        Assert.Equal(new[] { EstadoEnvio.Fallido, EstadoEnvio.EnviadoServidorSmtp }, despues.Envios.OrderBy(e => e.NumeroIntento).Select(e => e.Estado));
        Assert.Equal("otro.destino@ejemplo.test", Assert.Single(env.Correo.Enviados).Destinatario);
        Assert.EndsWith("(reenvío)", env.Correo.Enviados.Single().Asunto);
    }

    [Fact]
    public async Task Reenvio_a_correo_alternativo_invalido_se_rechaza()
    {
        var (env, docId) = await EmitirFacturaAsync();
        await using var _ = env;
        await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            env.ConAsync(sp => sp.GetRequiredService<CorreoService>().SolicitarEnvioAsync(docId, MotivoEnvio.Reenvio, "no-es-correo")));
    }

    [Fact]
    public async Task Archivo_faltante_impide_el_envio()
    {
        var (env, docId) = await EmitirFacturaAsync();
        await using var _ = env;
        var pdf = await env.DbAsync(db => db.ArchivosGenerados.SingleAsync(a => a.DocumentoId == docId && a.Proposito == PropositoArchivo.DocumentoPdf));
        File.Delete(Path.Combine(env.Carpeta, "Documentos", pdf.RutaRelativa));

        Assert.Equal(new[] { EstadoEnvio.Fallido }, await env.ProcesarCorreosAsync());
        Assert.Empty(env.Correo.Enviados);
        var envio = await env.DbAsync(db => db.EnviosCorreo.SingleAsync());
        Assert.Contains("no existe", envio.MensajeError);
    }

    [Fact]
    public async Task Archivo_modificado_despues_de_generarse_impide_el_envio()
    {
        var (env, docId) = await EmitirFacturaAsync();
        await using var _ = env;
        var json = await env.DbAsync(db => db.ArchivosGenerados.SingleAsync(a => a.DocumentoId == docId && a.Proposito == PropositoArchivo.DocumentoJson));
        await File.AppendAllTextAsync(Path.Combine(env.Carpeta, "Documentos", json.RutaRelativa), " ");

        Assert.Equal(new[] { EstadoEnvio.Fallido }, await env.ProcesarCorreosAsync());
        Assert.Empty(env.Correo.Enviados);
        Assert.Contains("fue modificado", (await env.DbAsync(db => db.EnviosCorreo.SingleAsync())).MensajeError);
    }

    [Fact]
    public async Task Un_envio_ya_procesado_no_se_vuelve_a_enviar()
    {
        var (env, _) = await EmitirFacturaAsync();
        await using var __ = env;
        var id = env.Cola.Encolados.Single();
        await env.ProcesarCorreosAsync();
        var estado = await env.ConAsync(sp => sp.GetRequiredService<CorreoService>().ProcesarAsync(id));
        Assert.Equal(EstadoEnvio.EnviadoServidorSmtp, estado);
        Assert.Single(env.Correo.Enviados);
    }

    [Fact]
    public async Task Almacen_no_sobrescribe_ni_permite_rutas_fuera_de_su_carpeta()
    {
        var (env, docId) = await EmitirFacturaAsync();
        await using var _ = env;
        var json = await env.DbAsync(db => db.ArchivosGenerados.SingleAsync(a => a.DocumentoId == docId && a.Proposito == PropositoArchivo.DocumentoJson));
        await env.ConAsync(async sp =>
        {
            var almacen = sp.GetRequiredService<IAlmacenArchivos>();
            await Assert.ThrowsAnyAsync<IOException>(() => almacen.GuardarNuevoAsync(json.RutaRelativa, new byte[] { 1 }));
            await Assert.ThrowsAsync<ArgumentException>(() => almacen.GuardarNuevoAsync("../fuera.json", new byte[] { 1 }));
            await Assert.ThrowsAsync<ArgumentException>(() => almacen.LeerAsync("../prueba.db"));
        });
        Assert.False(File.Exists(Path.Combine(env.Carpeta, "fuera.json")));
    }
}

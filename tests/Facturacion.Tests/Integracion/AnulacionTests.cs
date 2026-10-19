using Facturacion.Application.Servicios;
using Facturacion.Domain;
using Facturacion.Domain.Reglas;
using Facturacion.Tests.Soporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Facturacion.Tests.Integracion;

public class AnulacionTests
{
    private static Task<ResultadoAnulacion> AnularAsync(EntornoPruebas env, SolicitudAnulacion s) =>
        env.ConAsync(sp => sp.GetRequiredService<AnulacionService>().SolicitarAsync(s));

    [Fact]
    public async Task Escenario_D_anulacion_academica_devuelve_inventario_y_notifica_despues_de_procesar()
    {
        await using var env = await EntornoPruebas.CrearAsync();
        var cf = await env.CrearClienteAsync(DatosSinteticos.ConsumidorFinal());
        var p = await env.CrearProductoAsync(DatosSinteticos.Producto(inventario: true, existencias: 10));
        var fe = await env.EmitirAsync(DatosSinteticos.Venta(TipoDte.FacturaConsumidorFinal, cf.Id, (p.Id, 4, 0)));
        await env.ProcesarCorreosAsync();
        env.Reloj.Avanzar(TimeSpan.FromDays(20));

        var r = await AnularAsync(env, DatosSinteticos.Anulacion(fe.DocumentoId));

        Assert.Equal(EstadoAnulacion.ProcesadaSimulada, r.Estado);
        Assert.Contains("(simulación)", r.Mensaje);
        var doc = await env.DbAsync(db => db.Documentos.Include(d => d.Archivos).SingleAsync(d => d.Id == fe.DocumentoId));
        Assert.Equal(EstadoDocumento.Anulado, doc.Estado);
        Assert.Equal(10m, await env.DbAsync(db => db.Productos.Where(x => x.Id == p.Id).Select(x => x.Existencias).SingleAsync()));

        // Evento de anulación v2 guardado, sin sello de recepción.
        var evento = Assert.Single(doc.Archivos, a => a.Proposito == PropositoArchivo.AnulacionJson);
        var json = await File.ReadAllTextAsync(Path.Combine(env.Carpeta, "Documentos", evento.RutaRelativa));
        using var parsed = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(2, parsed.RootElement.GetProperty("identificacion").GetProperty("version").GetInt32());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, parsed.RootElement.GetProperty("documento").GetProperty("selloRecibido").ValueKind);

        // La notificación se registró después del procesamiento y adjunta el evento.
        Assert.NotNull(r.EnvioId);
        Assert.Equal(new[] { EstadoEnvio.EnviadoServidorSmtp }, await env.ProcesarCorreosAsync());
        var notificacion = env.Correo.Enviados.Last();
        Assert.StartsWith("Anulación de", notificacion.Asunto);
        Assert.Equal(evento.NombreArchivo, Assert.Single(notificacion.Adjuntos).NombreArchivo);
        Assert.Contains("No es una invalidación oficial", notificacion.CuerpoTexto);
    }

    [Fact]
    public async Task Doble_anulacion_se_rechaza()
    {
        await using var env = await EntornoPruebas.CrearAsync();
        var cf = await env.CrearClienteAsync(DatosSinteticos.ConsumidorFinal());
        var p = await env.CrearProductoAsync(DatosSinteticos.Producto());
        var fe = await env.EmitirAsync(DatosSinteticos.Venta(TipoDte.FacturaConsumidorFinal, cf.Id, (p.Id, 1, 0)));
        await AnularAsync(env, DatosSinteticos.Anulacion(fe.DocumentoId));

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => AnularAsync(env, DatosSinteticos.Anulacion(fe.DocumentoId)));
        Assert.Contains("ya fue anulado", ex.Message);
        Assert.Equal(1, await env.DbAsync(db => db.Anulaciones.CountAsync()));
    }

    [Fact]
    public async Task Ccf_fuera_del_plazo_configurado_no_se_anula_y_queda_en_auditoria()
    {
        await using var env = await EntornoPruebas.CrearAsync();
        var co = await env.CrearClienteAsync(DatosSinteticos.Contribuyente());
        var p = await env.CrearProductoAsync(DatosSinteticos.Producto());
        var ccf = await env.EmitirAsync(DatosSinteticos.Venta(TipoDte.ComprobanteCreditoFiscal, co.Id, (p.Id, 1, 0)));
        env.Reloj.Avanzar(TimeSpan.FromDays(2));

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => AnularAsync(env, DatosSinteticos.Anulacion(ccf.DocumentoId)));
        Assert.Contains("venció", ex.Message);
        Assert.Equal(EstadoDocumento.Emitido, await env.DbAsync(db => db.Documentos.Where(d => d.Id == ccf.DocumentoId).Select(d => d.Estado).SingleAsync()));
        Assert.Contains("Anulación rechazada por validación", await env.DbAsync(db => db.Auditoria.Select(a => a.Accion).ToListAsync()));
        Assert.Empty(env.Correo.Enviados.Where(m => m.Asunto.StartsWith("Anulación")));
    }

    [Fact]
    public async Task Plazo_es_configurable()
    {
        await using var env = await EntornoPruebas.CrearAsync(new() { ["Anulaciones:CreditoFiscal:Valor"] = "3", ["Anulaciones:CreditoFiscal:Unidad"] = "Meses" });
        var co = await env.CrearClienteAsync(DatosSinteticos.Contribuyente());
        var p = await env.CrearProductoAsync(DatosSinteticos.Producto());
        var ccf = await env.EmitirAsync(DatosSinteticos.Venta(TipoDte.ComprobanteCreditoFiscal, co.Id, (p.Id, 1, 0)));
        env.Reloj.Avanzar(TimeSpan.FromDays(30));

        var r = await AnularAsync(env, DatosSinteticos.Anulacion(ccf.DocumentoId));
        Assert.Equal(EstadoAnulacion.ProcesadaSimulada, r.Estado);
    }

    [Fact]
    public async Task Tipo_error_en_informacion_exige_documento_de_reemplazo()
    {
        await using var env = await EntornoPruebas.CrearAsync();
        var cf = await env.CrearClienteAsync(DatosSinteticos.ConsumidorFinal());
        var p = await env.CrearProductoAsync(DatosSinteticos.Producto());
        var original = await env.EmitirAsync(DatosSinteticos.Venta(TipoDte.FacturaConsumidorFinal, cf.Id, (p.Id, 1, 0)));

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            AnularAsync(env, DatosSinteticos.Anulacion(original.DocumentoId, TipoAnulacion.ErrorEnInformacion)));
        Assert.Contains("reemplaza", ex.Message);

        var reemplazo = await env.EmitirAsync(DatosSinteticos.Venta(TipoDte.FacturaConsumidorFinal, cf.Id, (p.Id, 2, 0)));
        var r = await AnularAsync(env, DatosSinteticos.Anulacion(original.DocumentoId, TipoAnulacion.ErrorEnInformacion, reemplazo.DocumentoId));
        Assert.Equal(EstadoAnulacion.ProcesadaSimulada, r.Estado);
        var anulacion = await env.DbAsync(db => db.Anulaciones.SingleAsync());
        Assert.Equal(reemplazo.DocumentoId, anulacion.DocumentoReemplazoId);
    }

    [Fact]
    public async Task Ccf_con_nota_de_credito_vigente_no_se_anula_ni_admite_nc_anulado()
    {
        await using var env = await EntornoPruebas.CrearAsync();
        var co = await env.CrearClienteAsync(DatosSinteticos.Contribuyente());
        var p = await env.CrearProductoAsync(DatosSinteticos.Producto());
        var ccf = await env.EmitirAsync(DatosSinteticos.Venta(TipoDte.ComprobanteCreditoFiscal, co.Id, (p.Id, 2, 0)));
        var linea = await env.DbAsync(db => db.DetallesDocumento.SingleAsync(d => d.DocumentoId == ccf.DocumentoId));
        var nc = await env.EmitirAsync(new SolicitudEmision
        {
            Tipo = TipoDte.NotaCredito, DocumentoRelacionadoId = ccf.DocumentoId, FormaPago = "01",
            LineasNotaCredito = { new LineaNotaCreditoSolicitud { DetalleRelacionadoId = linea.Id, Cantidad = 1 } }
        });

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => AnularAsync(env, DatosSinteticos.Anulacion(ccf.DocumentoId)));
        Assert.Contains("notas de crédito vigentes", ex.Message);

        // Al anular la nota de crédito, el CCF puede anularse y después ya no admite notas de crédito.
        await AnularAsync(env, DatosSinteticos.Anulacion(nc.DocumentoId));
        await AnularAsync(env, DatosSinteticos.Anulacion(ccf.DocumentoId));
        var ex2 = await Assert.ThrowsAsync<ReglaNegocioException>(() => env.EmitirAsync(new SolicitudEmision
        {
            Tipo = TipoDte.NotaCredito, DocumentoRelacionadoId = ccf.DocumentoId, FormaPago = "01",
            LineasNotaCredito = { new LineaNotaCreditoSolicitud { DetalleRelacionadoId = linea.Id, Cantidad = 1 } }
        }));
        Assert.Contains("anulado", ex2.Message);
    }

    [Fact]
    public async Task Notificacion_de_anulacion_no_procesada_se_rechaza()
    {
        await using var env = await EntornoPruebas.CrearAsync();
        var cf = await env.CrearClienteAsync(DatosSinteticos.ConsumidorFinal());
        var p = await env.CrearProductoAsync(DatosSinteticos.Producto());
        var fe = await env.EmitirAsync(DatosSinteticos.Venta(TipoDte.FacturaConsumidorFinal, cf.Id, (p.Id, 1, 0)));

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => env.ConAsync(sp =>
            sp.GetRequiredService<CorreoService>().SolicitarEnvioAsync(fe.DocumentoId, MotivoEnvio.NotificacionAnulacion, null, 999)));
        Assert.Contains("ya fue procesada", ex.Message);
    }

    [Fact]
    public async Task Datos_del_solicitante_son_obligatorios()
    {
        await using var env = await EntornoPruebas.CrearAsync();
        var cf = await env.CrearClienteAsync(DatosSinteticos.ConsumidorFinal());
        var p = await env.CrearProductoAsync(DatosSinteticos.Producto());
        var fe = await env.EmitirAsync(DatosSinteticos.Venta(TipoDte.FacturaConsumidorFinal, cf.Id, (p.Id, 1, 0)));
        var s = DatosSinteticos.Anulacion(fe.DocumentoId);
        s.NombreSolicita = "";
        s.NumDocResponsable = "123";

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => AnularAsync(env, s));
        Assert.Contains(ex.Resultado.Errores, e => e.Campo == "NombreSolicita");
        Assert.Contains(ex.Resultado.Errores, e => e.Campo == "NumDocResponsable");
    }
}

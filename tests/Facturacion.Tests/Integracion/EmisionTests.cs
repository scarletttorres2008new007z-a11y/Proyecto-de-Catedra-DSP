using System.Text.Json;
using Facturacion.Application.Abstracciones;
using Facturacion.Application.Servicios;
using Facturacion.Domain;
using Facturacion.Domain.Reglas;
using Facturacion.Infrastructure.Persistencia;
using Facturacion.Tests.Soporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig;

namespace Facturacion.Tests.Integracion;

/// <summary>Emisión completa sobre una base SQLite temporal y aislada (no toca la base de SQL Server).</summary>
public class EmisionTests
{
    private static async Task<(EntornoPruebas Env, int ConsumidorId, int ContribuyenteId, int ProductoId)> PrepararAsync(bool inventario = false, decimal existencias = 0)
    {
        var env = await EntornoPruebas.CrearAsync();
        var cf = await env.CrearClienteAsync(DatosSinteticos.ConsumidorFinal());
        var co = await env.CrearClienteAsync(DatosSinteticos.Contribuyente());
        var p = await env.CrearProductoAsync(DatosSinteticos.Producto(precio: 10m, inventario: inventario, existencias: existencias));
        return (env, cf.Id, co.Id, p.Id);
    }

    private static async Task<JsonElement> LeerJsonAsync(EntornoPruebas env, int documentoId)
    {
        var archivo = await env.DbAsync(db => db.ArchivosGenerados.SingleAsync(a => a.DocumentoId == documentoId && a.Proposito == PropositoArchivo.DocumentoJson));
        var bytes = await env.ConAsync(sp => sp.GetRequiredService<IAlmacenArchivos>().LeerAsync(archivo.RutaRelativa));
        return JsonDocument.Parse(bytes!).RootElement.Clone();
    }

    [Fact]
    public async Task Escenario_A_factura_genera_documento_json_pdf_y_envio_pendiente()
    {
        var (env, cf, _, prod) = await PrepararAsync();
        await using var _ = env;

        var r = await env.EmitirAsync(DatosSinteticos.Venta(TipoDte.FacturaConsumidorFinal, cf, (prod, 3, 0)));
        Assert.False(r.YaExistia);

        var doc = await env.DbAsync(db => db.Documentos.Include(d => d.Archivos).Include(d => d.Envios).Include(d => d.Detalles).SingleAsync(d => d.Id == r.DocumentoId));
        var codigo = doc.CodigoGeneracion.ToString().ToUpperInvariant();
        Assert.Equal("DTE-01-M001P001-000000000000001", doc.NumeroControl);
        Assert.Equal(33.90m, doc.TotalPagar);
        Assert.Equal(3.90m, doc.TotalIva);
        Assert.Equal("TREINTA Y TRES 90/100 DÓLARES", doc.TotalLetras);
        Assert.Equal("PRUEBA Consumidor Final", doc.ReceptorNombre); // instantánea del receptor

        // Archivos con nombre = código de generación, fuera de wwwroot y con huella registrada.
        Assert.Equal(new[] { $"{codigo}.json", $"{codigo}.pdf" }, doc.Archivos.OrderBy(a => a.Proposito).Select(a => a.NombreArchivo));
        Assert.All(doc.Archivos, a =>
        {
            Assert.Equal(64, a.Sha256.Length);
            Assert.StartsWith("2026/03/", a.RutaRelativa);
            Assert.True(File.Exists(Path.Combine(env.Carpeta, "Documentos", a.RutaRelativa)));
        });

        var envio = Assert.Single(doc.Envios);
        Assert.Equal(EstadoEnvio.Pendiente, envio.Estado);
        Assert.Equal("consumidor@ejemplo.test", envio.Destinatario);
        Assert.Contains(envio.Id, env.Cola.Encolados);

        var auditoria = await env.DbAsync(db => db.Auditoria.Select(a => a.Accion).ToListAsync());
        Assert.Contains("Documento emitido", auditoria);
        Assert.Contains("Transmisión simulada (modo académico)", auditoria);
    }

    [Fact]
    public async Task Json_de_factura_cumple_estructura_v1_sin_sello_y_con_aviso_academico()
    {
        var (env, cf, _, prod) = await PrepararAsync();
        await using var _ = env;
        var r = await env.EmitirAsync(DatosSinteticos.Venta(TipoDte.FacturaConsumidorFinal, cf, (prod, 3, 0)));
        var json = await LeerJsonAsync(env, r.DocumentoId);

        var id = json.GetProperty("identificacion");
        Assert.Equal(1, id.GetProperty("version").GetInt32());
        Assert.Equal("01", id.GetProperty("tipoDte").GetString());
        Assert.Equal("00", id.GetProperty("ambiente").GetString());
        Assert.Matches("^DTE-01-[A-Z0-9]{8}-[0-9]{15}$", id.GetProperty("numeroControl").GetString());
        Assert.Matches("^[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12}$", id.GetProperty("codigoGeneracion").GetString());
        Assert.Equal("2026-03-10", id.GetProperty("fecEmi").GetString());
        Assert.Equal("10:00:00", id.GetProperty("horEmi").GetString());

        var item = json.GetProperty("cuerpoDocumento")[0];
        Assert.Equal(11.30m, item.GetProperty("precioUni").GetDecimal());
        Assert.Equal(3.90m, item.GetProperty("ivaItem").GetDecimal());
        var resumen = json.GetProperty("resumen");
        Assert.Equal(33.90m, resumen.GetProperty("totalPagar").GetDecimal());
        Assert.Equal(3.90m, resumen.GetProperty("totalIva").GetDecimal());
        Assert.Equal("00000000000017", json.GetProperty("emisor").GetProperty("nit").GetString());

        var texto = json.GetRawText();
        Assert.DoesNotContain("sello", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("firma", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("modoAcademico", json.GetProperty("apendice").GetRawText());
    }

    [Fact]
    public async Task Escenario_B_credito_fiscal_informa_iva_como_tributo_20()
    {
        var (env, _, co, prod) = await PrepararAsync();
        await using var _ = env;
        var r = await env.EmitirAsync(DatosSinteticos.Venta(TipoDte.ComprobanteCreditoFiscal, co, (prod, 3, 2)));
        var json = await LeerJsonAsync(env, r.DocumentoId);

        Assert.Equal(3, json.GetProperty("identificacion").GetProperty("version").GetInt32());
        Assert.Equal("03", json.GetProperty("identificacion").GetProperty("tipoDte").GetString());
        var receptor = json.GetProperty("receptor");
        Assert.Equal("00000000000025", receptor.GetProperty("nit").GetString());
        Assert.Equal("100002", receptor.GetProperty("nrc").GetString());
        Assert.Equal("20", json.GetProperty("cuerpoDocumento")[0].GetProperty("tributos")[0].GetString());
        var tributo = json.GetProperty("resumen").GetProperty("tributos")[0];
        Assert.Equal("20", tributo.GetProperty("codigo").GetString());
        Assert.Equal(3.64m, tributo.GetProperty("valor").GetDecimal());
        Assert.Equal(31.64m, json.GetProperty("resumen").GetProperty("totalPagar").GetDecimal());
    }

    [Fact]
    public async Task Credito_fiscal_a_consumidor_final_se_rechaza_sin_guardar_nada()
    {
        var (env, cf, _, prod) = await PrepararAsync();
        await using var _ = env;
        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            env.EmitirAsync(DatosSinteticos.Venta(TipoDte.ComprobanteCreditoFiscal, cf, (prod, 1, 0))));
        Assert.Contains("contribuyente", ex.Message);
        Assert.Equal(0, await env.DbAsync(db => db.Documentos.CountAsync()));
        Assert.False(Directory.Exists(Path.Combine(env.Carpeta, "Documentos", "2026")) &&
                     Directory.EnumerateFiles(Path.Combine(env.Carpeta, "Documentos", "2026"), "*", SearchOption.AllDirectories).Any());
    }

    [Fact]
    public async Task Escenario_C_nota_de_credito_relaciona_el_ccf_y_controla_cantidades()
    {
        var (env, _, co, prod) = await PrepararAsync();
        await using var _ = env;
        var ccf = await env.EmitirAsync(DatosSinteticos.Venta(TipoDte.ComprobanteCreditoFiscal, co, (prod, 5, 0)));
        var lineaCcf = await env.DbAsync(db => db.DetallesDocumento.SingleAsync(d => d.DocumentoId == ccf.DocumentoId));

        SolicitudEmision Nc(decimal cantidad) => new()
        {
            Tipo = TipoDte.NotaCredito,
            DocumentoRelacionadoId = ccf.DocumentoId,
            FormaPago = "01",
            LineasNotaCredito = { new LineaNotaCreditoSolicitud { DetalleRelacionadoId = lineaCcf.Id, Cantidad = cantidad } }
        };

        var nc = await env.EmitirAsync(Nc(2));
        var json = await LeerJsonAsync(env, nc.DocumentoId);
        Assert.Equal("05", json.GetProperty("identificacion").GetProperty("tipoDte").GetString());
        var rel = json.GetProperty("documentoRelacionado")[0];
        Assert.Equal("03", rel.GetProperty("tipoDocumento").GetString());
        Assert.Equal(2, rel.GetProperty("tipoGeneracion").GetInt32());
        var ccfDoc = await env.DbAsync(db => db.Documentos.SingleAsync(d => d.Id == ccf.DocumentoId));
        Assert.Equal(ccfDoc.CodigoGeneracion.ToString().ToUpperInvariant(), rel.GetProperty("numeroDocumento").GetString());
        Assert.Equal(22.60m, json.GetProperty("resumen").GetProperty("montoTotalOperacion").GetDecimal()); // 20.00 + 2.60
        Assert.Equal("DTE-05-M001P001-000000000000001", json.GetProperty("identificacion").GetProperty("numeroControl").GetString());

        // Quedan 3 unidades: acreditar 4 se rechaza; 3 se acepta.
        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => env.EmitirAsync(Nc(4)));
        Assert.Contains("solo quedan 3", ex.Message);
        await env.EmitirAsync(Nc(3));
        await Assert.ThrowsAsync<ReglaNegocioException>(() => env.EmitirAsync(Nc(0.5m)));
    }

    [Fact]
    public async Task Nota_de_credito_sobre_factura_se_rechaza()
    {
        var (env, cf, _, prod) = await PrepararAsync();
        await using var _ = env;
        var fe = await env.EmitirAsync(DatosSinteticos.Venta(TipoDte.FacturaConsumidorFinal, cf, (prod, 1, 0)));
        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => env.EmitirAsync(new SolicitudEmision
        {
            Tipo = TipoDte.NotaCredito, DocumentoRelacionadoId = fe.DocumentoId, FormaPago = "01"
        }));
        Assert.Contains("comprobante de crédito fiscal", ex.Message);
    }

    [Fact]
    public async Task Misma_clave_de_idempotencia_no_duplica_el_documento()
    {
        var (env, cf, _, prod) = await PrepararAsync(inventario: true, existencias: 10);
        await using var _ = env;
        var clave = Guid.NewGuid();
        var s = DatosSinteticos.Venta(TipoDte.FacturaConsumidorFinal, cf, (prod, 2, 0));

        var primero = await env.EmitirAsync(s, clave);
        var segundo = await env.EmitirAsync(s, clave);

        Assert.True(segundo.YaExistia);
        Assert.Equal(primero.DocumentoId, segundo.DocumentoId);
        Assert.Equal(1, await env.DbAsync(db => db.Documentos.CountAsync()));
        Assert.Equal(1, await env.DbAsync(db => db.EnviosCorreo.CountAsync()));
        Assert.Equal(8m, await env.DbAsync(db => db.Productos.Where(p => p.Id == prod).Select(p => p.Existencias).SingleAsync()));
    }

    [Fact]
    public async Task Correlativos_son_consecutivos_por_tipo()
    {
        var (env, cf, co, prod) = await PrepararAsync();
        await using var _ = env;
        for (var i = 0; i < 3; i++) await env.EmitirAsync(DatosSinteticos.Venta(TipoDte.FacturaConsumidorFinal, cf, (prod, 1, 0)));
        await env.EmitirAsync(DatosSinteticos.Venta(TipoDte.ComprobanteCreditoFiscal, co, (prod, 1, 0)));

        var numeros = await env.DbAsync(db => db.Documentos.OrderBy(d => d.Id).Select(d => d.NumeroControl).ToListAsync());
        Assert.Equal(new[]
        {
            "DTE-01-M001P001-000000000000001", "DTE-01-M001P001-000000000000002", "DTE-01-M001P001-000000000000003",
            "DTE-03-M001P001-000000000000001"
        }, numeros);
        Assert.Equal(4, numeros.Distinct().Count());
    }

    [Fact]
    public async Task Inventario_insuficiente_rechaza_la_emision_y_no_descuenta()
    {
        var (env, cf, _, prod) = await PrepararAsync(inventario: true, existencias: 2);
        await using var _ = env;
        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            env.EmitirAsync(DatosSinteticos.Venta(TipoDte.FacturaConsumidorFinal, cf, (prod, 1.5m, 0), (prod, 1, 0))));
        Assert.Contains("Existencias insuficientes", ex.Message);
        Assert.Equal(2m, await env.DbAsync(db => db.Productos.Where(p => p.Id == prod).Select(p => p.Existencias).SingleAsync()));

        await env.EmitirAsync(DatosSinteticos.Venta(TipoDte.FacturaConsumidorFinal, cf, (prod, 1.5m, 0)));
        Assert.Equal(0.5m, await env.DbAsync(db => db.Productos.Where(p => p.Id == prod).Select(p => p.Existencias).SingleAsync()));
    }

    [Fact]
    public async Task Producto_inactivo_o_sin_emisor_configurado_no_se_emite()
    {
        var env = await EntornoPruebas.CrearAsync(conEmisor: false);
        await using var _ = env;
        var cf = await env.CrearClienteAsync(DatosSinteticos.ConsumidorFinal());
        var p = await env.CrearProductoAsync(DatosSinteticos.Producto());
        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => env.EmitirAsync(DatosSinteticos.Venta(TipoDte.FacturaConsumidorFinal, cf.Id, (p.Id, 1, 0))));
        Assert.Contains("emisor", ex.Message);

        await env.ConAsync(sp => sp.GetRequiredService<ProductoService>().CambiarEstadoAsync(p.Id, false));
        ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => env.EmitirAsync(DatosSinteticos.Venta(TipoDte.FacturaConsumidorFinal, cf.Id, (p.Id, 1, 0))));
        Assert.Contains("inactivo", ex.Message);
    }

    [Fact]
    public async Task Cambiar_el_precio_del_producto_no_altera_documentos_emitidos()
    {
        var (env, cf, _, prod) = await PrepararAsync();
        await using var _ = env;
        var r = await env.EmitirAsync(DatosSinteticos.Venta(TipoDte.FacturaConsumidorFinal, cf, (prod, 1, 0)));
        await env.ConAsync(async sp =>
        {
            var svc = sp.GetRequiredService<ProductoService>();
            var p = (await svc.ObtenerAsync(prod))!;
            p.PrecioSinIva = 99m;
            await svc.ActualizarAsync(p);
        });
        var doc = await env.DbAsync(db => db.Documentos.Include(d => d.Detalles).SingleAsync(d => d.Id == r.DocumentoId));
        Assert.Equal(11.30m, doc.Detalles.Single().PrecioUnitario);
        Assert.Equal(11.30m, doc.TotalPagar);
    }

    [Fact]
    public async Task Pdf_contiene_codigo_numero_de_control_y_aviso_academico()
    {
        var (env, _, co, prod) = await PrepararAsync();
        await using var _ = env;
        var r = await env.EmitirAsync(DatosSinteticos.Venta(TipoDte.ComprobanteCreditoFiscal, co, (prod, 1, 0)));
        var doc = await env.DbAsync(db => db.Documentos.Include(d => d.Archivos).SingleAsync(d => d.Id == r.DocumentoId));
        var pdf = await env.ConAsync(sp => sp.GetRequiredService<IAlmacenArchivos>().LeerAsync(doc.Archivos.Single(a => a.Proposito == PropositoArchivo.DocumentoPdf).RutaRelativa));

        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(pdf!, 0, 5));
        using var lector = PdfDocument.Open(pdf);
        var texto = string.Join(" ", lector.GetPages().Select(p => p.Text));
        Assert.Contains(doc.CodigoGeneracion.ToString().ToUpperInvariant(), texto);
        Assert.Contains(doc.NumeroControl, texto);
        Assert.Contains("PRUEBA Contribuyente", texto);
        Assert.Contains("académica", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sello de recepción: No existe (simulación)", texto); // nunca un sello inventado
    }

    [Fact]
    public async Task Error_al_escribir_archivos_revierte_toda_la_emision()
    {
        var (env, cf, _, prod) = await PrepararAsync(inventario: true, existencias: 5);
        await using var _ = env;
        // Bloquea la carpeta del mes con un ARCHIVO del mismo nombre: la escritura del JSON falla.
        var carpetaMes = Path.Combine(env.Carpeta, "Documentos", "2026", "03");
        Directory.CreateDirectory(Path.GetDirectoryName(carpetaMes)!);
        await File.WriteAllTextAsync(carpetaMes, "bloqueo");

        await Assert.ThrowsAnyAsync<Exception>(() => env.EmitirAsync(DatosSinteticos.Venta(TipoDte.FacturaConsumidorFinal, cf, (prod, 1, 0))));

        Assert.Equal(0, await env.DbAsync(db => db.Documentos.CountAsync()));
        Assert.Equal(0, await env.DbAsync(db => db.EnviosCorreo.CountAsync()));
        Assert.Equal(5m, await env.DbAsync(db => db.Productos.Where(p => p.Id == prod).Select(p => p.Existencias).SingleAsync()));
        Assert.Equal(0L, await env.DbAsync(db => db.Correlativos.Select(c => c.Ultimo).FirstOrDefaultAsync()));
        Assert.Contains("Emisión fallida", await env.DbAsync(db => db.Auditoria.Select(a => a.Accion).ToListAsync()));
    }
}

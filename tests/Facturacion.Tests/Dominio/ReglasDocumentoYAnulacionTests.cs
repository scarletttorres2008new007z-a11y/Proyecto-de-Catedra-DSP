using Facturacion.Domain;
using Facturacion.Domain.Entidades;
using Facturacion.Domain.Reglas;
using Facturacion.Tests.Soporte;

namespace Facturacion.Tests.Dominio;

public class ReglasDocumentoTests
{
    [Fact]
    public void Credito_fiscal_exige_cliente_contribuyente()
    {
        var r = ReglasDocumento.ValidarReceptor(TipoDte.ComprobanteCreditoFiscal, DatosSinteticos.ConsumidorFinal(), 10m, 25000m);
        Assert.Contains("contribuyente", r.ToString());

        var c = DatosSinteticos.Contribuyente();
        ReglasCliente.Normalizar(c);
        Assert.True(ReglasDocumento.ValidarReceptor(TipoDte.ComprobanteCreditoFiscal, c, 10m, 25000m).EsValido);
    }

    [Fact]
    public void Factura_exige_identificacion_desde_el_umbral()
    {
        var anonimo = new Cliente { Tipo = TipoCliente.ConsumidorFinal, Nombre = "PRUEBA", Activo = true };
        Assert.True(ReglasDocumento.ValidarReceptor(TipoDte.FacturaConsumidorFinal, anonimo, 24999.99m, 25000m).EsValido);
        Assert.False(ReglasDocumento.ValidarReceptor(TipoDte.FacturaConsumidorFinal, anonimo, 25000m, 25000m).EsValido);
    }

    [Fact]
    public void Cliente_desactivado_no_puede_ser_receptor()
    {
        var c = DatosSinteticos.ConsumidorFinal();
        c.Activo = false;
        Assert.False(ReglasDocumento.ValidarReceptor(TipoDte.FacturaConsumidorFinal, c, 1m, 25000m).EsValido);
    }

    [Fact]
    public void Nota_de_credito_solo_sobre_ccf_vigente()
    {
        Assert.False(ReglasDocumento.ValidarDocumentoBaseNotaCredito(null, false).EsValido);
        Assert.False(ReglasDocumento.ValidarDocumentoBaseNotaCredito(new DocumentoTributario { Tipo = TipoDte.FacturaConsumidorFinal }, false).EsValido);
        Assert.False(ReglasDocumento.ValidarDocumentoBaseNotaCredito(
            new DocumentoTributario { Tipo = TipoDte.ComprobanteCreditoFiscal, Estado = EstadoDocumento.Anulado }, false).EsValido);
        Assert.False(ReglasDocumento.ValidarDocumentoBaseNotaCredito(
            new DocumentoTributario { Tipo = TipoDte.ComprobanteCreditoFiscal, Estado = EstadoDocumento.Emitido }, true).EsValido);
        Assert.True(ReglasDocumento.ValidarDocumentoBaseNotaCredito(
            new DocumentoTributario { Tipo = TipoDte.ComprobanteCreditoFiscal, Estado = EstadoDocumento.Emitido }, false).EsValido);
    }

    [Fact]
    public void Nota_de_credito_no_acredita_mas_de_lo_disponible()
    {
        var linea = new DetalleDocumento { NumItem = 1, Cantidad = 5, Descripcion = "PRUEBA" };
        Assert.True(ReglasDocumento.ValidarCantidadesNotaCredito(new[] { (linea, 2m, 3m) }).EsValido);
        var r = ReglasDocumento.ValidarCantidadesNotaCredito(new[] { (linea, 2m, 3.0001m) });
        Assert.Contains("solo quedan 3", r.ToString());
        Assert.False(ReglasDocumento.ValidarCantidadesNotaCredito(new[] { (linea, 0m, 0m) }).EsValido);
    }
}

public class ReglasAnulacionTests
{
    private static readonly PlazoAnulacion TresMeses = new(3, UnidadPlazo.Meses);
    private static readonly PlazoAnulacion UnDia = new(1, UnidadPlazo.Dias);

    private static DocumentoTributario Doc(TipoDte tipo, DateOnly fecha, int id = 1) =>
        new() { Id = id, Tipo = tipo, FechaEmision = fecha, Estado = EstadoDocumento.Emitido };

    private static ResultadoValidacion Validar(DocumentoTributario d, DateTime ahora, PlazoAnulacion plazo,
        TipoAnulacion tipo = TipoAnulacion.RescindirOperacion, string? motivo = "PRUEBA motivo válido",
        DocumentoTributario? reemplazo = null, bool vigente = false, bool notas = false) =>
        ReglasAnulacion.Validar(d, ahora, plazo, tipo, motivo, reemplazo, vigente, notas);

    [Fact]
    public void Plazo_en_meses_vence_al_final_del_dia_y_respeta_fin_de_mes()
    {
        Assert.Equal(new DateTime(2026, 4, 30, 23, 59, 59), ReglasAnulacion.FechaLimiteLocal(new DateOnly(2026, 1, 31), TresMeses));
        Assert.Equal(new DateTime(2026, 3, 11, 23, 59, 59), ReglasAnulacion.FechaLimiteLocal(new DateOnly(2026, 3, 10), UnDia));
    }

    [Fact]
    public void Rechaza_anulacion_fuera_de_plazo()
    {
        var ccf = Doc(TipoDte.ComprobanteCreditoFiscal, new DateOnly(2026, 3, 10));
        Assert.True(Validar(ccf, new DateTime(2026, 3, 11, 23, 59, 0), UnDia).EsValido);
        var r = Validar(ccf, new DateTime(2026, 3, 12, 0, 0, 1), UnDia);
        Assert.Contains("venció", r.ToString());
    }

    [Fact]
    public void Rechaza_doble_anulacion()
    {
        var d = Doc(TipoDte.FacturaConsumidorFinal, new DateOnly(2026, 3, 10));
        Assert.False(Validar(d, new DateTime(2026, 3, 10, 12, 0, 0), TresMeses, vigente: true).EsValido);
        d.Estado = EstadoDocumento.Anulado;
        Assert.False(Validar(d, new DateTime(2026, 3, 10, 12, 0, 0), TresMeses).EsValido);
    }

    [Fact]
    public void Tipos_1_y_3_exigen_documento_de_reemplazo_del_mismo_tipo_y_vigente()
    {
        var ahora = new DateTime(2026, 3, 10, 12, 0, 0);
        var d = Doc(TipoDte.FacturaConsumidorFinal, new DateOnly(2026, 3, 10));
        Assert.Contains("requiere indicar el documento", Validar(d, ahora, TresMeses, TipoAnulacion.ErrorEnInformacion).ToString());
        Assert.Contains("mismo tipo", Validar(d, ahora, TresMeses, TipoAnulacion.Otro,
            reemplazo: Doc(TipoDte.ComprobanteCreditoFiscal, new DateOnly(2026, 3, 10), 2)).ToString());
        Assert.Contains("mismo documento", Validar(d, ahora, TresMeses, TipoAnulacion.Otro, reemplazo: d).ToString());
        Assert.True(Validar(d, ahora, TresMeses, TipoAnulacion.ErrorEnInformacion,
            reemplazo: Doc(TipoDte.FacturaConsumidorFinal, new DateOnly(2026, 3, 10), 2)).EsValido);
    }

    [Fact]
    public void Rescindir_no_admite_reemplazo_y_exige_motivo()
    {
        var ahora = new DateTime(2026, 3, 10, 12, 0, 0);
        var d = Doc(TipoDte.FacturaConsumidorFinal, new DateOnly(2026, 3, 10));
        Assert.False(Validar(d, ahora, TresMeses, reemplazo: Doc(TipoDte.FacturaConsumidorFinal, new DateOnly(2026, 3, 10), 2)).EsValido);
        Assert.False(Validar(d, ahora, TresMeses, motivo: "abc").EsValido);
        Assert.False(Validar(d, ahora, TresMeses, motivo: null).EsValido);
    }

    [Fact]
    public void Ccf_con_notas_de_credito_vigentes_no_se_anula()
    {
        var ccf = Doc(TipoDte.ComprobanteCreditoFiscal, new DateOnly(2026, 3, 10));
        Assert.Contains("notas de crédito vigentes", Validar(ccf, new DateTime(2026, 3, 10, 12, 0, 0), UnDia, notas: true).ToString());
    }
}

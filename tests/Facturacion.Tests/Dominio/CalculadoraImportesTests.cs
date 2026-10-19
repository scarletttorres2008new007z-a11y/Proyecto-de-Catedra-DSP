using Facturacion.Domain;
using Facturacion.Domain.Reglas;

namespace Facturacion.Tests.Dominio;

public class CalculadoraImportesTests
{
    private static ResumenCalculado Calcular(TipoDte tipo, params LineaEntrada[] lineas) => CalculadoraImportes.Calcular(tipo, lineas);

    [Fact]
    public void Factura_incluye_iva_en_el_precio_y_desglosa_iva_por_linea()
    {
        var r = Calcular(TipoDte.FacturaConsumidorFinal, new LineaEntrada(3, 10.00m, 0));

        var l = Assert.Single(r.Lineas);
        Assert.Equal(11.30m, l.PrecioUnitario);
        Assert.Equal(33.90m, l.VentaGravada);
        Assert.Equal(3.90m, l.IvaItem);
        Assert.Equal(33.90m, r.TotalGravada);
        Assert.Equal(3.90m, r.TotalIva);
        Assert.Equal(33.90m, r.MontoTotalOperacion);
        Assert.Equal(33.90m, r.TotalPagar);
    }

    [Fact]
    public void Factura_redondea_precio_con_iva_a_dos_decimales()
    {
        var r = Calcular(TipoDte.FacturaConsumidorFinal, new LineaEntrada(7, 0.99m, 0));

        Assert.Equal(1.12m, r.Lineas[0].PrecioUnitario); // 0.99 × 1.13 = 1.1187
        Assert.Equal(7.84m, r.TotalPagar);
        Assert.Equal(0.90m, r.TotalIva);                 // 7.84 × 0.13 / 1.13 = 0.9019…
    }

    [Fact]
    public void Credito_fiscal_suma_iva_sobre_el_total_gravado_con_descuento()
    {
        var r = Calcular(TipoDte.ComprobanteCreditoFiscal, new LineaEntrada(3, 10.00m, 2.00m));

        Assert.Equal(10.00m, r.Lineas[0].PrecioUnitario);
        Assert.Equal(28.00m, r.TotalGravada);
        Assert.Equal(2.00m, r.TotalDescuento);
        Assert.Equal(3.64m, r.TotalIva);
        Assert.Equal(31.64m, r.MontoTotalOperacion);
        Assert.Equal(31.64m, r.TotalPagar);
        Assert.Equal(0m, r.Lineas[0].IvaItem);
    }

    [Fact]
    public void Redondeo_de_punto_medio_se_aleja_de_cero()
    {
        // 0.50 × 0.13 = 0.065 → 0.07 (con redondeo bancario sería 0.06).
        var r = Calcular(TipoDte.ComprobanteCreditoFiscal, new LineaEntrada(1, 0.50m, 0));
        Assert.Equal(0.07m, r.TotalIva);
    }

    [Fact]
    public void Credito_fiscal_calcula_iva_sobre_el_total_y_no_por_linea()
    {
        var r = Calcular(TipoDte.ComprobanteCreditoFiscal,
            new LineaEntrada(1, 0.05m, 0), new LineaEntrada(1, 0.05m, 0), new LineaEntrada(1, 0.05m, 0));
        // Por línea: 3 × redondear(0.0065) = 0.03; sobre el total: redondear(0.15 × 0.13 = 0.0195) = 0.02.
        Assert.Equal(0.15m, r.TotalGravada);
        Assert.Equal(0.02m, r.TotalIva);
        Assert.Equal(0.17m, r.TotalPagar);
    }

    [Fact]
    public void Cantidades_con_decimales_se_aceptan_hasta_cuatro()
    {
        var r = Calcular(TipoDte.ComprobanteCreditoFiscal, new LineaEntrada(1.5m, 2.25m, 0));
        Assert.Equal(3.38m, r.TotalGravada); // 3.375 → 3.38
    }

    [Theory]
    [InlineData(0, 1, 0, "cantidad debe ser mayor que cero")]
    [InlineData(-2, 1, 0, "cantidad debe ser mayor que cero")]
    [InlineData(1.23456, 1, 0, "4 decimales")]
    [InlineData(1, 0, 0, "precio debe ser mayor que cero")]
    [InlineData(1, 10, -1, "descuento no puede ser negativo")]
    [InlineData(1, 10, 0.001, "descuento admite 2 decimales")]
    [InlineData(1, 10, 10.01, "no puede superar el importe")]
    public void Rechaza_lineas_invalidas(double cantidad, double precio, double descuento, string mensaje)
    {
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            Calcular(TipoDte.ComprobanteCreditoFiscal, new LineaEntrada((decimal)cantidad, (decimal)precio, (decimal)descuento)));
        Assert.Contains(mensaje, ex.Message);
    }

    [Fact]
    public void Rechaza_documento_sin_lineas_o_con_demasiadas()
    {
        Assert.Throws<ReglaNegocioException>(() => Calcular(TipoDte.FacturaConsumidorFinal));
        var muchas = Enumerable.Range(0, CalculadoraImportes.MaxLineas + 1).Select(_ => new LineaEntrada(1, 1, 0)).ToArray();
        var ex = Assert.Throws<ReglaNegocioException>(() => Calcular(TipoDte.FacturaConsumidorFinal, muchas));
        Assert.Contains("como máximo", ex.Message);
    }

    [Fact]
    public void Factura_suma_iva_de_cada_linea()
    {
        var r = Calcular(TipoDte.FacturaConsumidorFinal, new LineaEntrada(1, 1.00m, 0), new LineaEntrada(2, 2.50m, 0.50m));
        // L1: 1.13 → IVA 0.13. L2: precio 2.83, bruto 5.66, gravada 5.16 → IVA 0.59.
        Assert.Equal(new[] { 0.13m, 0.59m }, r.Lineas.Select(l => l.IvaItem));
        Assert.Equal(0.72m, r.TotalIva);
        Assert.Equal(6.29m, r.TotalPagar);
    }
}

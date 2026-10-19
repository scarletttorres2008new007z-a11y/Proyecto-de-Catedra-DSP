using Facturacion.Domain.Reglas;

namespace Facturacion.Tests.Dominio;

public class NumeroALetrasTests
{
    [Theory]
    [InlineData("120.50", "CIENTO VEINTE 50/100 DÓLARES")]
    [InlineData("0.75", "CERO 75/100 DÓLARES")]
    [InlineData("100", "CIEN 00/100 DÓLARES")]
    [InlineData("45.10", "CUARENTA Y CINCO 10/100 DÓLARES")]
    [InlineData("1001", "MIL UNO 00/100 DÓLARES")]
    [InlineData("21000", "VEINTIÚN MIL 00/100 DÓLARES")]
    [InlineData("1000000", "UN MILLÓN 00/100 DÓLARES")]
    [InlineData("2500001.99", "DOS MILLONES QUINIENTOS MIL UNO 99/100 DÓLARES")]
    [InlineData("33.905", "TREINTA Y TRES 91/100 DÓLARES")]
    public void Convierte_importes(string monto, string esperado) =>
        Assert.Equal(esperado, NumeroALetras.Convertir(decimal.Parse(monto, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Rechaza_negativos() => Assert.Throws<ArgumentOutOfRangeException>(() => NumeroALetras.Convertir(-1));
}

using Facturacion.Domain;
using Facturacion.Domain.Entidades;
using Facturacion.Domain.Reglas;
using Facturacion.Tests.Soporte;

namespace Facturacion.Tests.Dominio;

public class IdentificadoresYClientesTests
{
    [Theory]
    [InlineData("123456789", "12345678-9")]
    [InlineData(" 12345678-9 ", "12345678-9")]
    public void Normaliza_dui(string entrada, string esperado)
    {
        var v = IdentificadoresFiscales.NormalizarDui(entrada);
        Assert.Equal(esperado, v);
        Assert.True(IdentificadoresFiscales.EsDuiValido(v));
    }

    [Theory]
    [InlineData("0000-000000-000-1", true)]
    [InlineData("00000000000017", true)]
    [InlineData("000000001", true)]       // DUI homologado como NIT
    [InlineData("0000000000001", false)]  // 13 dígitos
    [InlineData("0000000000ABCD", false)]
    public void Valida_nit(string entrada, bool valido) =>
        Assert.Equal(valido, IdentificadoresFiscales.EsNitValido(IdentificadoresFiscales.NormalizarNit(entrada)));

    [Theory]
    [InlineData("1234-5", true)]
    [InlineData("123456789", false)]
    [InlineData("12A", false)]
    public void Valida_nrc(string entrada, bool valido) =>
        Assert.Equal(valido, IdentificadoresFiscales.EsNrcValido(IdentificadoresFiscales.NormalizarNrc(entrada)));

    [Theory]
    [InlineData("cliente@ejemplo.test", true)]
    [InlineData("sin-arroba.ejemplo.test", false)]
    [InlineData("dos@@ejemplo.test", false)]
    [InlineData("con espacio@ejemplo.test", false)]
    public void Valida_correo(string correo, bool valido) => Assert.Equal(valido, IdentificadoresFiscales.EsCorreoValido(correo));

    private static ResultadoValidacion Validar(Cliente c)
    {
        ReglasCliente.Normalizar(c);
        return ReglasCliente.Validar(c);
    }

    [Fact]
    public void Consumidor_final_solo_requiere_nombre()
    {
        var r = Validar(new Cliente { Tipo = TipoCliente.ConsumidorFinal, Nombre = "  PRUEBA Cliente  " });
        Assert.True(r.EsValido, r.ToString());
    }

    [Fact]
    public void Contribuyente_sintetico_completo_es_valido_y_se_normaliza()
    {
        var c = DatosSinteticos.Contribuyente(nit: "0000-000000-002-5", correo: "CONTRIBUYENTE@Ejemplo.Test");
        var r = Validar(c);
        Assert.True(r.EsValido, r.ToString());
        Assert.Equal("00000000000025", c.NumeroDocumento);
        Assert.Equal(Catalogos.DocNit, c.TipoDocumento);
        Assert.Equal("contribuyente@ejemplo.test", c.Correo);
    }

    [Fact]
    public void Contribuyente_sin_nrc_actividad_direccion_ni_correo_es_invalido()
    {
        var r = Validar(new Cliente { Tipo = TipoCliente.Contribuyente, Nombre = "PRUEBA", NumeroDocumento = "00000000000025" });
        var campos = r.Errores.Select(e => e.Campo).ToHashSet();
        Assert.Contains(nameof(Cliente.Nrc), campos);
        Assert.Contains(nameof(Cliente.CodActividad), campos);
        Assert.Contains(nameof(Cliente.Departamento), campos);
        Assert.Contains(nameof(Cliente.Correo), campos);
    }

    [Fact]
    public void Direccion_parcial_y_dui_mal_formado_se_rechazan()
    {
        var r = Validar(new Cliente
        {
            Tipo = TipoCliente.ConsumidorFinal, Nombre = "PRUEBA", TipoDocumento = Catalogos.DocDui, NumeroDocumento = "1234",
            Departamento = "06"
        });
        var campos = r.Errores.Select(e => e.Campo).ToHashSet();
        Assert.Contains(nameof(Cliente.NumeroDocumento), campos);
        Assert.Contains(nameof(Cliente.Municipio), campos);
        Assert.Contains(nameof(Cliente.Complemento), campos);
    }

    [Fact]
    public void Nrc_solo_se_admite_con_nit()
    {
        var r = Validar(new Cliente
        {
            Tipo = TipoCliente.ConsumidorFinal, Nombre = "PRUEBA", TipoDocumento = Catalogos.DocDui, NumeroDocumento = "00000000-0", Nrc = "1234"
        });
        Assert.Contains(r.Errores, e => e.Campo == nameof(Cliente.Nrc));
    }
}

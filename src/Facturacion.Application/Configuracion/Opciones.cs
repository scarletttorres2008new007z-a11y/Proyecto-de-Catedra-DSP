using Facturacion.Domain;
using Facturacion.Domain.Reglas;

namespace Facturacion.Application.Configuracion;

/// <summary>Sección "Facturacion" de appsettings.</summary>
public sealed class OpcionesFacturacion
{
    public const string Seccion = "Facturacion";

    /// <summary>Academico (por defecto) u Oficial (no implementado).</summary>
    public ModoOperacion Modo { get; set; } = ModoOperacion.Academico;

    /// <summary>
    /// Monto a partir del cual la factura exige identificación del receptor. Debe verificarse con el esquema
    /// y la normativa vigentes.
    /// </summary>
    public decimal UmbralIdentificacionFactura { get; set; } = 25000m;
}

/// <summary>Sección "Anulaciones" de appsettings.</summary>
public sealed class OpcionesAnulacion
{
    public const string Seccion = "Anulaciones";

    public PlazoConfigurado Factura { get; set; } = new() { Valor = 3, Unidad = UnidadPlazo.Meses };
    public PlazoConfigurado CreditoFiscal { get; set; } = new() { Valor = 1, Unidad = UnidadPlazo.Dias };
    public PlazoConfigurado NotaCredito { get; set; } = new() { Valor = 1, Unidad = UnidadPlazo.Dias };

    public PlazoAnulacion Plazo(TipoDte tipo)
    {
        var p = tipo switch
        {
            TipoDte.FacturaConsumidorFinal => Factura,
            TipoDte.ComprobanteCreditoFiscal => CreditoFiscal,
            TipoDte.NotaCredito => NotaCredito,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
        return new PlazoAnulacion(p.Valor, p.Unidad);
    }
}

public sealed class PlazoConfigurado
{
    public int Valor { get; set; }
    public UnidadPlazo Unidad { get; set; }
}

/// <summary>Sección "Almacenamiento" de appsettings.</summary>
public sealed class OpcionesAlmacenamiento
{
    public const string Seccion = "Almacenamiento";

    /// <summary>Carpeta raíz de los archivos generados. Si es relativa, se resuelve desde la raíz de contenido de la app.</summary>
    public string RutaDocumentos { get; set; } = "App_Data/Documentos";
}

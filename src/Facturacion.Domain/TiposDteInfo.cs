namespace Facturacion.Domain;

/// <summary>Metadatos de cada tipo de DTE: código CAT-002, nombre y versión del esquema JSON utilizada.</summary>
public static class TiposDteInfo
{
    public static string Codigo(TipoDte tipo) => ((int)tipo).ToString("00");

    public static string Nombre(TipoDte tipo) => tipo switch
    {
        TipoDte.FacturaConsumidorFinal => "Factura de consumidor final electrónica",
        TipoDte.ComprobanteCreditoFiscal => "Comprobante de crédito fiscal electrónico",
        TipoDte.NotaCredito => "Nota de crédito electrónica",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo))
    };

    public static string NombreCorto(TipoDte tipo) => tipo switch
    {
        TipoDte.FacturaConsumidorFinal => "Factura",
        TipoDte.ComprobanteCreditoFiscal => "CCF",
        TipoDte.NotaCredito => "Nota de crédito",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo))
    };

    /// <summary>Versión del esquema JSON publicada por Hacienda que sigue la estructura generada (FE v1, CCF v3, NC v3).</summary>
    public static int VersionEsquema(TipoDte tipo) => tipo switch
    {
        TipoDte.FacturaConsumidorFinal => 1,
        TipoDte.ComprobanteCreditoFiscal => 3,
        TipoDte.NotaCredito => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo))
    };

    /// <summary>En la factura de consumidor final el precio unitario incluye IVA; en CCF y nota de crédito no.</summary>
    public static bool PrecioIncluyeIva(TipoDte tipo) => tipo == TipoDte.FacturaConsumidorFinal;
}

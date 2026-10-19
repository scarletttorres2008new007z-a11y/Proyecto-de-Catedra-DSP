namespace Facturacion.Application.Dte;

/// <summary>
/// Nota de crédito electrónica (tipo 05), estructura del esquema NC versión 3. A diferencia de la factura y
/// el CCF, exige documentos relacionados, no tiene códigos de establecimiento en el emisor ni pagos en el resumen.
/// </summary>
public sealed class NotaCreditoDte
{
    public IdentificacionDte Identificacion { get; init; } = new();
    public List<DocumentoRelacionadoDte> DocumentoRelacionado { get; init; } = new();
    public EmisorNotaCreditoDte Emisor { get; init; } = new();
    public ReceptorContribuyenteDte Receptor { get; init; } = new();
    public object? VentaTercero { get; init; }
    public List<ItemNotaCreditoDte> CuerpoDocumento { get; init; } = new();
    public ResumenNotaCreditoDte Resumen { get; init; } = new();
    public object? Extension { get; init; }
    public List<ApendiceDte>? Apendice { get; init; }
}

public sealed class ItemNotaCreditoDte
{
    public int NumItem { get; init; }
    public int TipoItem { get; init; }
    /// <summary>Código de generación del documento relacionado al que corresponde la línea.</summary>
    public string NumeroDocumento { get; init; } = "";
    public string? Codigo { get; init; }
    public string? CodTributo { get; init; }
    public string Descripcion { get; init; } = "";
    public decimal Cantidad { get; init; }
    public int UniMedida { get; init; }
    public decimal PrecioUni { get; init; }
    public decimal MontoDescu { get; init; }
    public decimal VentaNoSuj { get; init; }
    public decimal VentaExenta { get; init; }
    public decimal VentaGravada { get; init; }
    public List<string>? Tributos { get; init; }
}

public sealed class ResumenNotaCreditoDte
{
    public decimal TotalNoSuj { get; init; }
    public decimal TotalExenta { get; init; }
    public decimal TotalGravada { get; init; }
    public decimal SubTotalVentas { get; init; }
    public decimal DescuNoSuj { get; init; }
    public decimal DescuExenta { get; init; }
    public decimal DescuGravada { get; init; }
    public decimal TotalDescu { get; init; }
    public List<TributoResumenDte>? Tributos { get; init; }
    public decimal SubTotal { get; init; }
    public decimal IvaPerci1 { get; init; }
    public decimal IvaRete1 { get; init; }
    public decimal ReteRenta { get; init; }
    public decimal MontoTotalOperacion { get; init; }
    public string TotalLetras { get; init; } = "";
    public int CondicionOperacion { get; init; }
}

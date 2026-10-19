namespace Facturacion.Application.Dte;

/// <summary>Comprobante de crédito fiscal electrónico (tipo 03), estructura del esquema CCF versión 3.</summary>
public sealed class CreditoFiscalDte
{
    public IdentificacionDte Identificacion { get; init; } = new();
    public List<DocumentoRelacionadoDte>? DocumentoRelacionado { get; init; }
    public EmisorEstablecimientoDte Emisor { get; init; } = new();
    public ReceptorContribuyenteDte Receptor { get; init; } = new();
    public object? OtrosDocumentos { get; init; }
    public object? VentaTercero { get; init; }
    public List<ItemCreditoFiscalDte> CuerpoDocumento { get; init; } = new();
    public ResumenCreditoFiscalDte Resumen { get; init; } = new();
    public object? Extension { get; init; }
    public List<ApendiceDte>? Apendice { get; init; }
}

public sealed class ItemCreditoFiscalDte
{
    public int NumItem { get; init; }
    public int TipoItem { get; init; }
    public string? NumeroDocumento { get; init; }
    public string? Codigo { get; init; }
    public string? CodTributo { get; init; }
    public string Descripcion { get; init; } = "";
    public decimal Cantidad { get; init; }
    public int UniMedida { get; init; }
    /// <summary>Precio unitario sin IVA.</summary>
    public decimal PrecioUni { get; init; }
    public decimal MontoDescu { get; init; }
    public decimal VentaNoSuj { get; init; }
    public decimal VentaExenta { get; init; }
    public decimal VentaGravada { get; init; }
    /// <summary>Códigos de tributo aplicados a la línea gravada ("20" = IVA).</summary>
    public List<string>? Tributos { get; init; }
    public decimal Psv { get; init; }
    public decimal NoGravado { get; init; }
}

public sealed class ResumenCreditoFiscalDte
{
    public decimal TotalNoSuj { get; init; }
    public decimal TotalExenta { get; init; }
    public decimal TotalGravada { get; init; }
    public decimal SubTotalVentas { get; init; }
    public decimal DescuNoSuj { get; init; }
    public decimal DescuExenta { get; init; }
    public decimal DescuGravada { get; init; }
    public decimal PorcentajeDescuento { get; init; }
    public decimal TotalDescu { get; init; }
    public List<TributoResumenDte>? Tributos { get; init; }
    public decimal SubTotal { get; init; }
    public decimal IvaPerci1 { get; init; }
    public decimal IvaRete1 { get; init; }
    public decimal ReteRenta { get; init; }
    public decimal MontoTotalOperacion { get; init; }
    public decimal TotalNoGravado { get; init; }
    public decimal TotalPagar { get; init; }
    public string TotalLetras { get; init; } = "";
    public decimal SaldoFavor { get; init; }
    public int CondicionOperacion { get; init; }
    public List<PagoDte>? Pagos { get; init; }
    public string? NumPagoElectronico { get; init; }
}

namespace Facturacion.Application.Dte;

/// <summary>Factura de consumidor final electrónica (tipo 01), estructura del esquema FE versión 1.</summary>
public sealed class FacturaDte
{
    public IdentificacionDte Identificacion { get; init; } = new();
    public List<DocumentoRelacionadoDte>? DocumentoRelacionado { get; init; }
    public EmisorEstablecimientoDte Emisor { get; init; } = new();
    public ReceptorFacturaDte Receptor { get; init; } = new();
    public object? OtrosDocumentos { get; init; }
    public object? VentaTercero { get; init; }
    public List<ItemFacturaDte> CuerpoDocumento { get; init; } = new();
    public ResumenFacturaDte Resumen { get; init; } = new();
    public object? Extension { get; init; }
    public List<ApendiceDte>? Apendice { get; init; }
}

/// <summary>En la factura todos los datos del receptor son opcionales (nulos si no se registraron).</summary>
public sealed class ReceptorFacturaDte
{
    public string? TipoDocumento { get; init; }
    public string? NumDocumento { get; init; }
    public string? Nrc { get; init; }
    public string? Nombre { get; init; }
    public string? CodActividad { get; init; }
    public string? DescActividad { get; init; }
    public DireccionDte? Direccion { get; init; }
    public string? Telefono { get; init; }
    public string? Correo { get; init; }
}

public sealed class ItemFacturaDte
{
    public int NumItem { get; init; }
    public int TipoItem { get; init; }
    public string? NumeroDocumento { get; init; }
    public decimal Cantidad { get; init; }
    public string? Codigo { get; init; }
    public string? CodTributo { get; init; }
    public int UniMedida { get; init; }
    public string Descripcion { get; init; } = "";
    /// <summary>Precio unitario con IVA incluido.</summary>
    public decimal PrecioUni { get; init; }
    public decimal MontoDescu { get; init; }
    public decimal VentaNoSuj { get; init; }
    public decimal VentaExenta { get; init; }
    public decimal VentaGravada { get; init; }
    public List<string>? Tributos { get; init; }
    public decimal Psv { get; init; }
    public decimal NoGravado { get; init; }
    /// <summary>IVA incluido en la venta gravada de la línea.</summary>
    public decimal IvaItem { get; init; }
}

public sealed class ResumenFacturaDte
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
    public decimal IvaRete1 { get; init; }
    public decimal ReteRenta { get; init; }
    public decimal MontoTotalOperacion { get; init; }
    public decimal TotalNoGravado { get; init; }
    public decimal TotalPagar { get; init; }
    public string TotalLetras { get; init; } = "";
    public decimal TotalIva { get; init; }
    public decimal SaldoFavor { get; init; }
    public int CondicionOperacion { get; init; }
    public List<PagoDte>? Pagos { get; init; }
    public string? NumPagoElectronico { get; init; }
}

namespace Facturacion.Application.Dte;

// Estructuras compartidas por los esquemas JSON de los DTE (nombres de propiedad en camelCase al serializar).

public sealed class IdentificacionDte
{
    public int Version { get; init; }
    public string Ambiente { get; init; } = "";
    public string TipoDte { get; init; } = "";
    public string NumeroControl { get; init; } = "";
    public string CodigoGeneracion { get; init; } = "";
    /// <summary>CAT-003: 1 = modelo de facturación previo.</summary>
    public int TipoModelo { get; init; } = 1;
    /// <summary>CAT-004: 1 = transmisión normal.</summary>
    public int TipoOperacion { get; init; } = 1;
    public int? TipoContingencia { get; init; }
    public string? MotivoContin { get; init; }
    public string FecEmi { get; init; } = "";
    public string HorEmi { get; init; } = "";
    public string TipoMoneda { get; init; } = "USD";
}

public sealed class DireccionDte
{
    public string Departamento { get; init; } = "";
    public string Municipio { get; init; } = "";
    public string Complemento { get; init; } = "";
}

/// <summary>Emisor de factura y CCF (incluye códigos de establecimiento y punto de venta).</summary>
public sealed class EmisorEstablecimientoDte
{
    public string Nit { get; init; } = "";
    public string Nrc { get; init; } = "";
    public string Nombre { get; init; } = "";
    public string CodActividad { get; init; } = "";
    public string DescActividad { get; init; } = "";
    public string? NombreComercial { get; init; }
    public string TipoEstablecimiento { get; init; } = "";
    public DireccionDte Direccion { get; init; } = new();
    public string Telefono { get; init; } = "";
    public string Correo { get; init; } = "";
    /// <summary>Código asignado por Hacienda; no existe en modo académico.</summary>
    public string? CodEstableMH { get; init; }
    public string? CodEstable { get; init; }
    /// <summary>Código asignado por Hacienda; no existe en modo académico.</summary>
    public string? CodPuntoVentaMH { get; init; }
    public string? CodPuntoVenta { get; init; }
}

/// <summary>Emisor de la nota de crédito (el esquema no incluye códigos de establecimiento).</summary>
public sealed class EmisorNotaCreditoDte
{
    public string Nit { get; init; } = "";
    public string Nrc { get; init; } = "";
    public string Nombre { get; init; } = "";
    public string CodActividad { get; init; } = "";
    public string DescActividad { get; init; } = "";
    public string? NombreComercial { get; init; }
    public string TipoEstablecimiento { get; init; } = "";
    public DireccionDte Direccion { get; init; } = new();
    public string Telefono { get; init; } = "";
    public string Correo { get; init; } = "";
}

/// <summary>Receptor contribuyente (CCF y nota de crédito).</summary>
public sealed class ReceptorContribuyenteDte
{
    public string Nit { get; init; } = "";
    public string Nrc { get; init; } = "";
    public string Nombre { get; init; } = "";
    public string CodActividad { get; init; } = "";
    public string DescActividad { get; init; } = "";
    public string? NombreComercial { get; init; }
    public DireccionDte Direccion { get; init; } = new();
    public string? Telefono { get; init; }
    public string Correo { get; init; } = "";
}

public sealed class TributoResumenDte
{
    public string Codigo { get; init; } = "";
    public string Descripcion { get; init; } = "";
    public decimal Valor { get; init; }
}

public sealed class PagoDte
{
    public string Codigo { get; init; } = "";
    public decimal MontoPago { get; init; }
    public string? Referencia { get; init; }
    public string? Plazo { get; init; }
    public int? Periodo { get; init; }
}

public sealed class ApendiceDte
{
    public string Campo { get; init; } = "";
    public string Etiqueta { get; init; } = "";
    public string Valor { get; init; } = "";
}

public sealed class DocumentoRelacionadoDte
{
    public string TipoDocumento { get; init; } = "";
    public int TipoGeneracion { get; init; }
    public string NumeroDocumento { get; init; } = "";
    public string FechaEmision { get; init; } = "";
}

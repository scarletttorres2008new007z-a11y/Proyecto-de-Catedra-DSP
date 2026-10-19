namespace Facturacion.Application.Dte;

/// <summary>
/// Evento de invalidación, estructura del esquema de anulación versión 2. En modo académico el campo
/// selloRecibido es nulo porque el documento nunca fue recibido por Hacienda: este JSON no es válido para
/// transmisión oficial.
/// </summary>
public sealed class AnulacionDte
{
    public IdentificacionAnulacionDte Identificacion { get; init; } = new();
    public EmisorAnulacionDte Emisor { get; init; } = new();
    public DocumentoAnuladoDte Documento { get; init; } = new();
    public MotivoAnulacionDte Motivo { get; init; } = new();
}

public sealed class IdentificacionAnulacionDte
{
    public int Version { get; init; } = 2;
    public string Ambiente { get; init; } = "";
    public string CodigoGeneracion { get; init; } = "";
    public string FecAnula { get; init; } = "";
    public string HorAnula { get; init; } = "";
}

public sealed class EmisorAnulacionDte
{
    public string Nit { get; init; } = "";
    public string Nombre { get; init; } = "";
    public string TipoEstablecimiento { get; init; } = "";
    public string? NomEstablecimiento { get; init; }
    public string? CodEstableMH { get; init; }
    public string? CodEstable { get; init; }
    public string? CodPuntoVentaMH { get; init; }
    public string? CodPuntoVenta { get; init; }
    public string? Telefono { get; init; }
    public string Correo { get; init; } = "";
}

public sealed class DocumentoAnuladoDte
{
    public string TipoDte { get; init; } = "";
    public string CodigoGeneracion { get; init; } = "";
    /// <summary>Nulo en modo académico: no existe sello de recepción.</summary>
    public string? SelloRecibido { get; init; }
    public string NumeroControl { get; init; } = "";
    public string FecEmi { get; init; } = "";
    public decimal? MontoIva { get; init; }
    public string? CodigoGeneracionR { get; init; }
    public string? TipoDocumento { get; init; }
    public string? NumDocumento { get; init; }
    public string? Nombre { get; init; }
    public string? Telefono { get; init; }
    public string? Correo { get; init; }
}

public sealed class MotivoAnulacionDte
{
    public int TipoAnulacion { get; init; }
    public string? MotivoAnulacion { get; init; }
    public string NombreResponsable { get; init; } = "";
    public string TipDocResponsable { get; init; } = "";
    public string NumDocResponsable { get; init; } = "";
    public string NombreSolicita { get; init; } = "";
    public string TipDocSolicita { get; init; } = "";
    public string NumDocSolicita { get; init; } = "";
}

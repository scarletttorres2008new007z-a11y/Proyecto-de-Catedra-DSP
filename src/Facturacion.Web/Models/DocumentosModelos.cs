using Facturacion.Application.Servicios;
using Facturacion.Domain;
using Facturacion.Domain.Entidades;

namespace Facturacion.Web.Models;

/// <summary>Pantalla de emisión de factura o CCF.</summary>
public sealed class EmisionVista
{
    public SolicitudEmision Solicitud { get; set; } = new();
    public IReadOnlyList<Cliente> Clientes { get; set; } = Array.Empty<Cliente>();
    public IReadOnlyList<Producto> Productos { get; set; } = Array.Empty<Producto>();
    public bool EmisorConfigurado { get; set; }
}

/// <summary>Pantalla de nota de crédito sobre un CCF.</summary>
public sealed class NotaCreditoVista
{
    public SolicitudEmision Solicitud { get; set; } = new();
    public DocumentoTributario? Ccf { get; set; }
    public IReadOnlyList<LineaAcreditable> Lineas { get; set; } = Array.Empty<LineaAcreditable>();
    public IReadOnlyList<DocumentoTributario> CcfDisponibles { get; set; } = Array.Empty<DocumentoTributario>();
}

/// <summary>Revisión previa a la confirmación (importes calculados por el servidor).</summary>
public sealed class RevisionVista
{
    public SolicitudEmision Solicitud { get; set; } = new();
    public DocumentoPreparado Preparado { get; set; } = null!;
    public Guid ClaveIdempotencia { get; set; }
}

public sealed class DetalleDocumentoVista
{
    public DocumentoTributario Documento { get; set; } = null!;
    public EmisorDatos Emisor { get; set; } = null!;
    public IReadOnlyList<DocumentoTributario> NotasCredito { get; set; } = Array.Empty<DocumentoTributario>();
    public bool PuedeEmitirNotaCredito { get; set; }
    public ContextoAnulacion? Anulacion { get; set; }
}

public sealed record EmisorDatos(string Nombre, string? NombreComercial, string Nit, string Nrc);

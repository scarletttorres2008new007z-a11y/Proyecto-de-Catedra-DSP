namespace Facturacion.Domain.Entidades;

public class DetalleDocumento
{
    public int Id { get; set; }
    public int DocumentoId { get; set; }
    public DocumentoTributario? Documento { get; set; }
    public int NumItem { get; set; }

    /// <summary>Referencia al producto (puede quedar nula si el producto deja de existir; el snapshot se conserva).</summary>
    public int? ProductoId { get; set; }
    public Producto? Producto { get; set; }

    // Snapshot del producto al emitir.
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public int UnidadMedida { get; set; }

    public decimal Cantidad { get; set; }
    /// <summary>Precio unitario tal como figura en el documento (con IVA en factura, sin IVA en CCF/NC).</summary>
    public decimal PrecioUnitario { get; set; }
    public decimal MontoDescuento { get; set; }
    public decimal VentaGravada { get; set; }
    /// <summary>IVA incluido en la línea (solo factura de consumidor final).</summary>
    public decimal IvaItem { get; set; }

    /// <summary>En notas de crédito: línea del CCF que se acredita.</summary>
    public int? DetalleRelacionadoId { get; set; }
}
